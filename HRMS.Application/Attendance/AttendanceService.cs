using HRMS.Application.Common.Queries;
using System.Globalization;
using HRMS.Application.Attendance;
using HRMS.Application.Auditing;
using HRMS.Domain.Attendance;
using HRMS.Application.Common;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HRMS.Application.Attendance;

public sealed class AttendanceService(
    IAttendanceData dbContext,
    IAuditLogService auditLogService,
    IConfiguration configuration,
    ILogger<AttendanceService> logger) : IAttendanceService
{
    private const int MaximumPageSize = 100;
    public Task<AttendanceRecordDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.AttendanceRecords.ForRead().Where(r => r.AttendanceId == id && !r.Employee!.IsDeleted)
            .Select(ToDto()).SingleOrDefaultAsync(cancellationToken);
    private static readonly HashSet<string> ManualStatuses =
    [
        AttendanceRecord.PresentStatus,
        AttendanceRecord.LateStatus,
        AttendanceRecord.AbsentStatus,
        AttendanceRecord.HalfDayStatus
    ];

    private readonly TimeZoneInfo _businessTimeZone = ResolveTimeZone(
        configuration["Attendance:BusinessTimeZoneId"] ?? "Jordan Standard Time");

    private readonly TimeSpan _lateAfter = TimeSpan.TryParse(
        configuration["Attendance:LateAfterLocalTime"],
        CultureInfo.InvariantCulture,
        out var lateAfter)
            ? lateAfter
            : new TimeSpan(9, 15, 0);

    public async Task<AttendancePage> GetAttendanceListAsync(
        AttendanceFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = FilterRecords(filter);
        var pageSize = Math.Clamp(filter.PageSize, 1, MaximumPageSize);
        var totalCount = await query.CountAsync(cancellationToken);
        if (filter.ShowAll) pageSize = Math.Max(1, totalCount);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var page = Math.Clamp(filter.Page, 1, totalPages);
        var records = await query
            .OrderByDescending(record => record.WorkDate)
            .ThenBy(record => record.Employee!.EmployeeNumber)
            .ThenByDescending(record => record.CheckInUtc)
            .ThenByDescending(record => record.AttendanceId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToDto())
            .ToListAsync(cancellationToken);
        return new AttendancePage(records, totalCount, page, pageSize);
    }

    public async Task<AttendanceRecordDto?> GetTodayRecordAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        var workDate = GetBusinessDate(DateTime.UtcNow);
        return await dbContext.AttendanceRecords
            .ForRead()
            .Where(record =>
                record.EmployeeId == employeeId &&
                (record.WorkDate == workDate || (record.CheckInUtc != null && record.CheckOutUtc == null)) &&
                !record.Employee!.IsDeleted)
            .OrderBy(record => record.CheckOutUtc != null)
            .ThenByDescending(record => record.WorkDate)
            .ThenByDescending(record => record.CheckInUtc)
            .ThenByDescending(record => record.AttendanceId)
            .Select(ToDto())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AttendanceOperationResult> CheckInAsync(
        int employeeId,
        DateTime? timeUtc = null,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var checkInUtc = timeUtc ?? DateTime.UtcNow;
        if (checkInUtc.Kind != DateTimeKind.Utc)
        {
            return AttendanceOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business067"));
        }

        var workDate = GetBusinessDate(checkInUtc);
        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(
                candidate => candidate.EmployeeId == employeeId && !candidate.IsDeleted,
                cancellationToken);
        if (employee is null)
        {
            return AttendanceOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business014"));
        }

        if (workDate < employee.HireDate || workDate > GetBusinessDate(DateTime.UtcNow))
        {
            return AttendanceOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business022"));
        }

        if (await dbContext.AttendanceRecords.AnyAsync(record => record.EmployeeId == employeeId &&
            record.CheckInUtc != null && record.CheckOutUtc == null, cancellationToken))
            return AttendanceOperationResult.Failure(HRMS.Application.Common.Messages.Get("Extra001"));

        var localTime = TimeZoneInfo.ConvertTimeFromUtc(checkInUtc, _businessTimeZone).TimeOfDay;
        var record = AttendanceRecord.CreateCheckIn(
            employeeId,
            workDate,
            checkInUtc,
            notes,
            localTime > _lateAfter);
        dbContext.AttendanceRecords.Add(record);

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogService.RecordAsync(new AuditLogWriteRequest(
                "Attendance.CheckIn",
                "AttendanceRecord",
                record.AttendanceId.ToString(CultureInfo.InvariantCulture),
                [
                    new("EmployeeId", null, employeeId.ToString(CultureInfo.InvariantCulture)),
                    new("WorkDate", null, workDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    new("Status", null, record.Status),
                    new("CheckInUtc", null, checkInUtc.ToString("O", CultureInfo.InvariantCulture))
                ]), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return AttendanceOperationResult.Success(record.AttendanceId);
        }
        catch (DataWriteException exception) when (IsDuplicateAttendance(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return AttendanceOperationResult.Failure(HRMS.Application.Common.Messages.Get("Extra001"));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to record check-in for employee {EmployeeId}.", employeeId);
            throw;
        }
    }

    public async Task<AttendanceOperationResult> CheckOutAsync(
        int employeeId,
        DateTime? timeUtc = null,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var checkOutUtc = timeUtc ?? DateTime.UtcNow;
        if (checkOutUtc.Kind != DateTimeKind.Utc)
        {
            return AttendanceOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business066"));
        }

        var workDate = GetBusinessDate(checkOutUtc);
        var record = await dbContext.AttendanceRecords
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.EmployeeId == employeeId &&
                    candidate.CheckInUtc != null && candidate.CheckOutUtc == null &&
                    !candidate.Employee!.IsDeleted,
                cancellationToken);
        if (record is null)
        {
            return AttendanceOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business056"));
        }

        if (record.CheckOutUtc.HasValue)
        {
            return AttendanceOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business030"));
        }

        try
        {
            record.RecordCheckOut(checkOutUtc, notes);
        }
        catch (ArgumentException)
        {
            return AttendanceOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business061"));
        }
        catch (InvalidOperationException)
        {
            return AttendanceOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business053"));
        }

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.RecordAsync(new AuditLogWriteRequest(
            "Attendance.CheckOut",
            "AttendanceRecord",
            record.AttendanceId.ToString(CultureInfo.InvariantCulture),
            [
                new("CheckOutUtc", null, checkOutUtc.ToString("O", CultureInfo.InvariantCulture)),
                new("TotalHours", null, record.TotalHours.ToString("F2", CultureInfo.InvariantCulture))
            ]), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AttendanceOperationResult.Success(record.AttendanceId);
    }

    public async Task<AttendanceOperationResult> RecordManualEntryAsync(
        int employeeId,
        DateOnly workDate,
        DateTime? checkInUtc,
        DateTime? checkOutUtc,
        string status,
        string? notes,
        CancellationToken cancellationToken = default,
        int? attendanceId = null,
        byte[]? rowVersion = null)
    {
        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(
                candidate => candidate.EmployeeId == employeeId && !candidate.IsDeleted,
                cancellationToken);
        if (employee is null)
        {
            return AttendanceOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business014"));
        }

        if (workDate < employee.HireDate ||
            workDate > GetBusinessDate(DateTime.UtcNow) ||
            !ManualStatuses.Contains(status))
        {
            return AttendanceOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business017"));
        }

        if ((checkInUtc.HasValue && checkInUtc.Value.Kind != DateTimeKind.Utc) ||
            (checkOutUtc.HasValue && checkOutUtc.Value.Kind != DateTimeKind.Utc))
        {
            return AttendanceOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business065"));
        }

        if (checkInUtc.HasValue && GetBusinessDate(checkInUtc.Value) != workDate)
            return AttendanceOperationResult.Failure(HRMS.Application.Common.Messages.Get("CheckInWorkDate"));
        var matches = await dbContext.AttendanceRecords
            .Where(candidate => candidate.EmployeeId == employeeId && candidate.WorkDate == workDate &&
                (!attendanceId.HasValue || candidate.AttendanceId == attendanceId.Value))
            .Take(2).ToListAsync(cancellationToken);
        if (matches.Count > 1 || (attendanceId.HasValue && matches.Count == 0))
            return AttendanceOperationResult.Failure("اختر الفترة المطلوبة من سجل الحضور لتصحيحها. / Select the session from the attendance list to correct it.");
        var record = matches.SingleOrDefault();
        if (record is not null)
        {
            if (!attendanceId.HasValue)
                return AttendanceOperationResult.Failure("اختر تعديل بجانب الفترة الموجودة في جدول الحضور. / Choose Edit beside the existing attendance session.");
            if (rowVersion is null || !rowVersion.SequenceEqual(record.RowVersion))
                return AttendanceOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business034"));
            dbContext.ExpectVersion(record, rowVersion);
        }
        var excludedId = record?.AttendanceId ?? 0;
        if (checkInUtc.HasValue && await dbContext.AttendanceRecords.AnyAsync(other =>
                other.EmployeeId == employeeId && other.AttendanceId != excludedId && other.CheckInUtc != null &&
                (!checkOutUtc.HasValue || other.CheckInUtc < checkOutUtc.Value) &&
                (other.CheckOutUtc == null || other.CheckOutUtc > checkInUtc.Value), cancellationToken))
            return AttendanceOperationResult.Failure("الفترة تتداخل مع فترة حضور أخرى؛ صحّح الفترة الموجودة أولًا. / This interval overlaps another attendance session.");
        var isNew = record is null;
        var previousCheckIn = record?.CheckInUtc;
        var previousCheckOut = record?.CheckOutUtc;
        var previousStatus = record?.Status;
        var previousHours = record?.TotalHours ?? 0;

        try
        {
            if (record is null)
            {
                record = new AttendanceRecord(
                    employeeId,
                    workDate,
                    checkInUtc,
                    checkOutUtc,
                    status,
                    notes);
                dbContext.AttendanceRecords.Add(record);
            }
            else
            {
                record.UpdateManual(checkInUtc, checkOutUtc, status, notes);
            }
        }
        catch (ArgumentException)
        {
            return AttendanceOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business002"));
        }

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogService.RecordAsync(new AuditLogWriteRequest(
                isNew ? "Attendance.ManualCreated" : "Attendance.ManualUpdated",
                "AttendanceRecord",
                record.AttendanceId.ToString(CultureInfo.InvariantCulture),
                [
                    new("EmployeeId", null, employeeId.ToString(CultureInfo.InvariantCulture)),
                    new("WorkDate", null, workDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    new("Status", previousStatus, record.Status),
                    new("CheckInUtc", previousCheckIn?.ToString("O", CultureInfo.InvariantCulture),
                        checkInUtc?.ToString("O", CultureInfo.InvariantCulture)),
                    new("CheckOutUtc", previousCheckOut?.ToString("O", CultureInfo.InvariantCulture),
                        checkOutUtc?.ToString("O", CultureInfo.InvariantCulture)),
                    new("TotalHours", previousHours.ToString("F2", CultureInfo.InvariantCulture),
                        record.TotalHours.ToString("F2", CultureInfo.InvariantCulture))
                ]), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return AttendanceOperationResult.Success(record.AttendanceId);
        }
        catch (DataWriteException exception) when (IsDuplicateAttendance(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return AttendanceOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business068"));
        }
        catch (DataConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AttendanceOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business034"));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to correct attendance for employee {EmployeeId}.", employeeId);
            throw;
        }
    }

    public async Task<AttendanceSummaryDto> GetSummaryAsync(
        AttendanceFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = FilterRecords(filter);
        var values = await query
            .GroupBy(_ => 1)
            .Select(records => new
            {
                Total = records.Count(),
                Present = records.Count(record => record.Status == AttendanceRecord.PresentStatus),
                Late = records.Count(record => record.Status == AttendanceRecord.LateStatus),
                Absent = records.Count(record => record.Status == AttendanceRecord.AbsentStatus),
                HalfDay = records.Count(record => record.Status == AttendanceRecord.HalfDayStatus),
                Hours = records.Sum(record => record.TotalHours)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return values is null
            ? new AttendanceSummaryDto(0, 0, 0, 0, 0, 0)
            : new AttendanceSummaryDto(
                values.Total,
                values.Present,
                values.Late,
                values.Absent,
                values.HalfDay,
                values.Hours);
    }

    private IQueryable<AttendanceRecord> FilterRecords(AttendanceFilter filter)
    {
        var query = dbContext.AttendanceRecords
            .ForRead()
            .Where(record => !record.Employee!.IsDeleted);
        if (filter.EmployeeId is int employeeId)
        {
            query = query.Where(record => record.EmployeeId == employeeId);
        }
        if (filter.DepartmentId is int departmentId)
        {
            query = query.Where(record => record.Employee!.DepartmentId == departmentId);
        }
        if (filter.FromDate is DateOnly fromDate)
        {
            query = query.Where(record => record.WorkDate >= fromDate);
        }
        if (filter.ToDate is DateOnly toDate)
        {
            query = query.Where(record => record.WorkDate <= toDate);
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(record => record.Status == filter.Status);
        }
        return query;
    }

    private static System.Linq.Expressions.Expression<Func<AttendanceRecord, AttendanceRecordDto>> ToDto() =>
        record => new AttendanceRecordDto(
            record.AttendanceId,
            record.EmployeeId,
            record.Employee!.EmployeeNumber,
            record.Employee.FirstName + " " + record.Employee.LastName,
            record.Employee.DepartmentId,
            record.Employee.Department!.Name,
            record.WorkDate,
            record.CheckInUtc,
            record.CheckOutUtc,
            record.TotalHours,
            record.Status,
            record.Notes,
            record.TotalHours < 0 ||
            (record.CheckInUtc.HasValue && record.CheckInUtc.Value > DateTime.UtcNow) ||
            (record.CheckOutUtc.HasValue &&
             (!record.CheckInUtc.HasValue || record.CheckOutUtc.Value < record.CheckInUtc.Value)) ||
            (record.Status != AttendanceRecord.PresentStatus &&
             record.Status != AttendanceRecord.LateStatus &&
             record.Status != AttendanceRecord.AbsentStatus &&
             record.Status != AttendanceRecord.HalfDayStatus), record.RowVersion);

    private DateOnly GetBusinessDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, _businessTimeZone));

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new InvalidOperationException(
                $"The configured attendance time zone '{timeZoneId}' is not available.",
                exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new InvalidOperationException(
                $"The configured attendance time zone '{timeZoneId}' is invalid.",
                exception);
        }
    }

    private static bool IsDuplicateAttendance(DataWriteException exception) => exception.IsUnique;
}
