using System.Text;
using HRMS.Application.Attendance;
using HRMS.Application.Reporting;
using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Reporting;

public sealed class ReportingService(
    HRMSDbContext dbContext,
    IAttendanceService attendanceService,
    HRMS.Application.Common.IBusinessClock clock) : IReportingService
{
    public async Task<ReportFilterOptions> GetFilterOptionsAsync(int? departmentScope = null,
        CancellationToken cancellationToken = default)
    {
        var employees = dbContext.Employees.AsNoTracking()
            .Where(e => !e.IsDeleted && (departmentScope == null || e.DepartmentId == departmentScope));
        return new ReportFilterOptions
        {
            Departments = await dbContext.Departments.AsNoTracking()
                .Where(d => departmentScope == null || d.DepartmentId == departmentScope)
                .OrderBy(d => d.Name).Select(d => new ReportOption(d.DepartmentId, d.Name)).ToListAsync(cancellationToken),
            Positions = await dbContext.Positions.AsNoTracking()
                .Where(p => departmentScope == null || p.DepartmentId == null || p.DepartmentId == departmentScope)
                .OrderBy(p => p.Title).Select(p => new ReportOption(p.PositionId, p.Title)).ToListAsync(cancellationToken),
            Employees = await employees.OrderBy(e => e.EmployeeNumber)
                .Select(e => new ReportOption(e.EmployeeId, e.EmployeeNumber + " - " + e.FirstName + " " + e.LastName))
                .ToListAsync(cancellationToken),
            LeaveTypes = await dbContext.LeaveTypes.AsNoTracking().OrderBy(t => t.Name)
                .Select(t => new ReportOption(t.LeaveTypeId, t.Name)).ToListAsync(cancellationToken),
            EmploymentStatuses = await employees.Select(e => e.EmploymentStatus).Distinct()
                .OrderBy(s => s).ToListAsync(cancellationToken)
        };
    }

    private static readonly string[] ActiveStatusValues = ["Active", "نشط", "على رأس العمل"];
    public async Task<DashboardSummary> GetDashboardAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int? departmentId = null,
        bool includeRecentActivity = true,
        CancellationToken cancellationToken = default)
    {
        if (toDate < fromDate)
        {
            throw new ArgumentException("Dashboard end date cannot be before its start date.");
        }

        var activeEmployees = dbContext.Employees
            .AsNoTracking()
            .Where(employee => !employee.IsDeleted && ActiveStatusValues.Contains(employee.EmploymentStatus));
        if (departmentId is int activeDepartmentId)
        {
            activeEmployees = activeEmployees.Where(employee => employee.DepartmentId == activeDepartmentId);
        }
        var totalActive = await activeEmployees.CountAsync(cancellationToken);
        var newHiresQuery = dbContext.Employees
            .AsNoTracking()
            .Where(employee => !employee.IsDeleted && employee.HireDate >= fromDate && employee.HireDate <= toDate);
        if (departmentId is int newHireDepartmentId)
        {
            newHiresQuery = newHiresQuery.Where(employee => employee.DepartmentId == newHireDepartmentId);
        }
        var newHires = await newHiresQuery.CountAsync(cancellationToken);
        var statusQuery = dbContext.Employees
            .AsNoTracking()
            .Where(employee => !employee.IsDeleted);
        if (departmentId is int statusDepartmentId)
        {
            statusQuery = statusQuery.Where(employee => employee.DepartmentId == statusDepartmentId);
        }
        var statusGroups = await statusQuery
            .GroupBy(employee => employee.EmploymentStatus)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Status, group => group.Count, cancellationToken);
        var pendingLeaveQuery = dbContext.LeaveRequests
            .AsNoTracking()
            .Where(request => request.Status == "Pending" && !request.Employee!.IsDeleted);
        if (departmentId is int pendingDepartmentId)
        {
            pendingLeaveQuery = pendingLeaveQuery.Where(request => request.Employee!.DepartmentId == pendingDepartmentId);
        }
        var pendingLeaves = await pendingLeaveQuery.CountAsync(cancellationToken);
        var headcount = await GetDepartmentHeadcountReportAsync(departmentId, cancellationToken);
        var attendance = await attendanceService.GetSummaryAsync(
            new AttendanceFilter(DepartmentId: departmentId, FromDate: fromDate, ToDate: toDate),
            cancellationToken);
        var recentActivityQuery = dbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(log => log.OccurredAtUtc)
            .Take(10)
            .Select(log => new RecentHrActivity(
                log.AuditLogId,
                dbContext.Users.Where(user => user.Id == log.ActorUserId)
                    .Select(user => user.UserName)
                    .FirstOrDefault(),
                log.Action,
                log.EntityName,
                log.EntityId,
                log.OccurredAtUtc));
        IReadOnlyList<RecentHrActivity> recentActivity = includeRecentActivity
            ? await recentActivityQuery.ToListAsync(cancellationToken)
            : Array.Empty<RecentHrActivity>();

        return new DashboardSummary(
            totalActive,
            headcount,
            newHires,
            statusGroups,
            pendingLeaves,
            attendance,
            recentActivity);
    }

    public async Task<ReportPage<EmployeeReportItem>> GetEmployeeDirectoryReportAsync(
        EmployeeReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Employees
            .AsNoTracking()
            .Where(employee => !employee.IsDeleted);
        if (filter.DepartmentId is int departmentId)
        {
            query = query.Where(employee => employee.DepartmentId == departmentId);
        }
        if (filter.PositionId is int positionId)
        {
            query = query.Where(employee => employee.PositionId == positionId);
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(employee => employee.EmploymentStatus == filter.Status);
        }
        if (filter.HiredAfter is DateOnly hiredAfter)
        {
            query = query.Where(employee => employee.HireDate >= hiredAfter);
        }
        if (filter.HiredBefore is DateOnly hiredBefore)
        {
            query = query.Where(employee => employee.HireDate <= hiredBefore);
        }

        query = filter.SortBy?.ToLowerInvariant() switch
        {
            "name" => filter.SortDescending
                ? query.OrderByDescending(employee => employee.LastName).ThenByDescending(employee => employee.FirstName)
                : query.OrderBy(employee => employee.LastName).ThenBy(employee => employee.FirstName),
            "hiredate" => filter.SortDescending
                ? query.OrderByDescending(employee => employee.HireDate)
                : query.OrderBy(employee => employee.HireDate),
            "status" => filter.SortDescending
                ? query.OrderByDescending(employee => employee.EmploymentStatus)
                : query.OrderBy(employee => employee.EmploymentStatus),
            _ => filter.SortDescending
                ? query.OrderByDescending(employee => employee.EmployeeNumber)
                : query.OrderBy(employee => employee.EmployeeNumber)
        };
        return await PageAsync(query
            .Select(employee => new EmployeeReportItem(
                employee.EmployeeNumber,
                employee.FirstName + " " + employee.LastName,
                employee.WorkEmail,
                employee.PhoneNumber,
                employee.Department!.Name,
                employee.Position!.Title,
                employee.HireDate,
                employee.EmploymentStatus,
                employee.EmploymentType,
                employee.WorkLocation)),
            filter.Page,
            filter.PageSize,
            cancellationToken);
    }

    public async Task<ReportPage<AttendanceReportItem>> GetAttendanceReportAsync(
        AttendanceReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.AttendanceRecords
            .AsNoTracking()
            .Where(record => !record.Employee!.IsDeleted);
        if (filter.DepartmentId is int departmentId)
        {
            query = query.Where(record => record.Employee!.DepartmentId == departmentId);
        }
        if (filter.EmployeeId is int employeeId)
        {
            query = query.Where(record => record.EmployeeId == employeeId);
        }
        if (filter.FromDate is DateOnly fromDate)
        {
            query = query.Where(record => record.WorkDate >= fromDate);
        }
        if (filter.ToDate is DateOnly toDate)
        {
            query = query.Where(record => record.WorkDate <= toDate);
        }

        return await PageAsync(query
            .OrderByDescending(record => record.WorkDate)
            .ThenBy(record => record.Employee!.EmployeeNumber)
            .Select(record => new AttendanceReportItem(
                record.Employee!.EmployeeNumber,
                record.Employee.FirstName + " " + record.Employee.LastName,
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
                (record.Status != HRMS.Domain.Attendance.AttendanceRecord.PresentStatus &&
                 record.Status != HRMS.Domain.Attendance.AttendanceRecord.LateStatus &&
                 record.Status != HRMS.Domain.Attendance.AttendanceRecord.AbsentStatus &&
                 record.Status != HRMS.Domain.Attendance.AttendanceRecord.HalfDayStatus))),
            filter.Page,
            filter.PageSize,
            cancellationToken);
    }

    public async Task<ReportPage<LeaveReportItem>> GetLeaveReportAsync(
        LeaveReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.LeaveRequests
            .AsNoTracking()
            .Where(request => !request.Employee!.IsDeleted);
        if (filter.DepartmentId is int departmentId)
        {
            query = query.Where(request => request.Employee!.DepartmentId == departmentId);
        }
        if (filter.EmployeeId is int employeeId)
        {
            query = query.Where(request => request.EmployeeId == employeeId);
        }
        if (filter.LeaveTypeId is int leaveTypeId)
        {
            query = query.Where(request => request.LeaveTypeId == leaveTypeId);
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(request => request.Status == filter.Status);
        }
        if (filter.Year is int year)
        {
            if (year is < 2000 or > 2100)
            {
                throw new ArgumentOutOfRangeException(nameof(filter), "Leave report year must be between 2000 and 2100.");
            }
            var yearStart = new DateOnly(year, 1, 1);
            var yearEnd = new DateOnly(year, 12, 31);
            query = query.Where(request => request.StartDate <= yearEnd && request.EndDate >= yearStart);
        }

        return await PageAsync(query
            .OrderByDescending(request => request.AppliedAtUtc)
            .Select(request => new LeaveReportItem(
                request.Employee!.EmployeeNumber,
                request.Employee.FirstName + " " + request.Employee.LastName,
                request.Employee.Department!.Name,
                request.LeaveType!.Name,
                request.StartDate,
                request.EndDate,
                request.TotalDays,
                request.Status,
                request.AppliedAtUtc,
                request.ReviewNotes)),
            filter.Page,
            filter.PageSize,
            cancellationToken);
    }

    public async Task<IReadOnlyList<DepartmentHeadcountItem>> GetDepartmentHeadcountReportAsync(
        int? departmentId = null,
        CancellationToken cancellationToken = default) =>
        await dbContext.Departments
            .AsNoTracking()
            .Where(department => !departmentId.HasValue || department.DepartmentId == departmentId.Value)
            .OrderBy(department => department.Name)
            .Select(department => new DepartmentHeadcountItem(
                department.DepartmentId,
                department.Name,
                department.Manager == null
                    ? null
                    : department.Manager.FirstName + " " + department.Manager.LastName,
                department.Employees.Count(employee => !employee.IsDeleted),
                department.Employees.Count(employee => !employee.IsDeleted && ActiveStatusValues.Contains(employee.EmploymentStatus)),
                department.Employees.Count(employee => !employee.IsDeleted && !ActiveStatusValues.Contains(employee.EmploymentStatus)),
                dbContext.Positions.Count(position =>
                    position.DepartmentId == department.DepartmentId && position.IsActive)))
            .ToListAsync(cancellationToken);

    public async Task<LeaveDecisionSummary> GetLeaveDecisionSummaryAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int? departmentId = null,
        CancellationToken cancellationToken = default)
    {
        if (toDate < fromDate)
        {
            throw new ArgumentException("Decision report end date cannot be before its start date.");
        }

        var fromUtc = clock.StartOfDayUtc(fromDate);
        var toExclusiveUtc = toDate == DateOnly.MaxValue
            ? DateTime.MaxValue
            : clock.StartOfDayUtc(toDate.AddDays(1));
        var decisions = dbContext.LeaveRequests
            .AsNoTracking()
            .Where(request =>
                !request.Employee!.IsDeleted &&
                request.ReviewedAtUtc >= fromUtc &&
                request.ReviewedAtUtc < toExclusiveUtc &&
                (request.Status == "Approved" || request.Status == "Rejected"));
        if (departmentId is int scopedDepartmentId)
        {
            decisions = decisions.Where(request => request.Employee!.DepartmentId == scopedDepartmentId);
        }

        var counts = await decisions
            .GroupBy(request => request.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);

        return new LeaveDecisionSummary(
            counts.GetValueOrDefault("Approved"),
            counts.GetValueOrDefault("Rejected"),
            fromDate,
            toDate);
    }

    private static async Task<ReportPage<T>> PageAsync<T>(
        IQueryable<T> query,
        int requestedPage,
        int requestedPageSize,
        CancellationToken cancellationToken)
    {
        const int maximumPageSize = 100;
        var totalCount = await query.CountAsync(cancellationToken);
        var pageSize = Math.Clamp(requestedPageSize, 1, maximumPageSize);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var page = Math.Clamp(requestedPage, 1, totalPages);
        var items = await query.Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new ReportPage<T>(items, totalCount, page, pageSize);
    }

    public byte[] GenerateCsv<T>(
        IEnumerable<T> data,
        string[] headers,
        Func<T, string[]> rowMapper)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rowMapper);

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", headers.Select(EscapeCsv)));
        foreach (var item in data)
        {
            builder.AppendLine(string.Join(",", rowMapper(item).Select(EscapeCsv)));
        }
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    private static string EscapeCsv(string? value)
    {
        var safeValue = value ?? string.Empty;
        if (safeValue.Contains(',') || safeValue.Contains('"') ||
            safeValue.Contains('\r') || safeValue.Contains('\n'))
        {
            return $"\"{safeValue.Replace("\"", "\"\"")}\"";
        }
        return safeValue;
    }
}
