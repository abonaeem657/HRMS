using HRMS.Application.Common.Queries;
using System.Globalization;
using System.Data;
using HRMS.Application.Auditing;
using HRMS.Application.LeaveManagement;
using HRMS.Domain.LeaveManagement;
using HRMS.Application.Common;


using Microsoft.Extensions.Logging;

namespace HRMS.Application.LeaveManagement;

public sealed class LeaveService(
    ILeaveData dbContext,
    IAuditLogService auditLogService,
    ILogger<LeaveService> logger,
    HRMS.Application.Identity.IRecordAccess access) : ILeaveService
{
    public async Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(
        bool onlyActive = true,
        CancellationToken cancellationToken = default) =>
        await dbContext.LeaveTypes
            .ForRead()
            .Where(type => !onlyActive || type.IsActive)
            .OrderBy(type => type.Name)
            .Select(type => new LeaveTypeDto(
                type.LeaveTypeId,
                type.Name,
                type.NameAr,
                type.DefaultDaysPerYear,
                type.IsPaid,
                type.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LeaveRequestDto>> GetLeaveRequestsAsync(
        LeaveFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(dbContext.LeaveRequests.ForRead(), filter);
        query = query.OrderByDescending(request => request.AppliedAtUtc).ThenByDescending(request => request.LeaveRequestId);
        if (filter.Page is int page)
            query = query.Skip((Math.Clamp(page, 1, 100000) - 1) * Math.Clamp(filter.PageSize, 1, 100)).Take(Math.Clamp(filter.PageSize, 1, 100));
        return await query
            .Select(ToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<LeaveRequestDto?> GetLeaveRequestByIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        await dbContext.LeaveRequests
            .ForRead()
            .Where(request => request.LeaveRequestId == id && !request.Employee!.IsDeleted)
            .Select(ToDto())
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<LeaveOperationResult> SubmitLeaveRequestAsync(
        CreateLeaveRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!await access.CanAccessEmployeeAsync(command.EmployeeId, false, cancellationToken))
            return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Extra000"));
        if (command.StartDate == command.EndDate && command.StartTime.HasValue && command.EndTime.HasValue && command.EndTime <= command.StartTime)
            return LeaveOperationResult.Failure("وقت النهاية يجب أن يكون بعد البداية عند تحديد الوقتين. / End time must follow start time when both are specified.");
        if (command.StartDate == default || command.EndDate < command.StartDate ||
            string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length > 1000)
        {
            return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business018"));
        }

        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(
                candidate => candidate.EmployeeId == command.EmployeeId && !candidate.IsDeleted,
                cancellationToken);
        if (employee is null || command.StartDate < employee.HireDate)
        {
            return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business013"));
        }

        if (!await dbContext.LeaveTypes.AnyAsync(
                type => type.LeaveTypeId == command.LeaveTypeId && type.IsActive,
                cancellationToken))
        {
            return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business059"));
        }

        await using var transaction = await dbContext.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var overlaps = await dbContext.LeaveRequests.AnyAsync(request =>
            request.EmployeeId == command.EmployeeId &&
            (request.Status == "Pending" || request.Status == "Approved") &&
            request.StartDate <= command.EndDate &&
            request.EndDate >= command.StartDate,
            cancellationToken);
        if (overlaps)
        {
            return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business062"));
        }

        var request = new LeaveRequest(
            command.EmployeeId,
            command.LeaveTypeId,
            command.StartDate,
            command.EndDate,
            command.Reason, command.StartTime, command.EndTime);
        dbContext.LeaveRequests.Add(request);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogService.RecordAsync(new AuditLogWriteRequest(
                "Leave.Submitted",
                "LeaveRequest",
                request.LeaveRequestId.ToString(CultureInfo.InvariantCulture),
                [
                    new("EmployeeId", null, command.EmployeeId.ToString(CultureInfo.InvariantCulture)),
                    new("LeaveTypeId", null, command.LeaveTypeId.ToString(CultureInfo.InvariantCulture)),
                    new("StartDate", null, command.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    new("EndDate", null, command.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    new("Status", null, request.Status)
                ]), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return LeaveOperationResult.Success(request.LeaveRequestId);
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(exception, "Failed to submit leave request for employee {EmployeeId}.", command.EmployeeId);
            throw;
        }
    }

    public Task<LeaveOperationResult> ApproveLeaveRequestAsync(
        int id,
        string reviewerUserId,
        string? notes = null,
        CancellationToken cancellationToken = default) =>
        ReviewAsync(id, reviewerUserId, notes, approve: true, cancellationToken);

    public Task<LeaveOperationResult> RejectLeaveRequestAsync(
        int id,
        string reviewerUserId,
        string reason,
        CancellationToken cancellationToken = default) =>
        ReviewAsync(id, reviewerUserId, reason, approve: false, cancellationToken);

    public async Task<LeaveOperationResult> CancelLeaveRequestAsync(
        int id,
        string cancelledByUserId,
        CancellationToken cancellationToken = default)
    {
        var request = await dbContext.LeaveRequests
            .SingleOrDefaultAsync(item => item.LeaveRequestId == id, cancellationToken);
        if (request is null)
        {
            return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business043"));
        }

        if (cancelledByUserId != access.UserId ||
            !await dbContext.Employees.AnyAsync(e => e.EmployeeId == request.EmployeeId && e.IdentityUserId == access.UserId, cancellationToken))
            return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Extra000"));

        var previousStatus = request.Status;
        try
        {
            request.Cancel(cancelledByUserId);
        }
        catch (InvalidOperationException)
        {
            return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business051"));
        }

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogService.RecordAsync(new AuditLogWriteRequest(
                "Leave.Cancelled",
                "LeaveRequest",
                id.ToString(CultureInfo.InvariantCulture),
                [new("Status", previousStatus, request.Status)]), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return LeaveOperationResult.Success(id);
        }
        catch (DataConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business034"));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to cancel leave request {LeaveRequestId}.", id);
            throw;
        }
    }

    public async Task<IReadOnlyList<LeaveBalanceDto>> GetEmployeeLeaveBalancesAsync(
        int employeeId,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (year is < 2000 or > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "Leave balance year must be between 2000 and 2100.");
        }

        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);
        var types = await dbContext.LeaveTypes
            .ForRead()
            .Where(type => type.IsActive)
            .Select(type => new
            {
                type.LeaveTypeId,
                type.Name,
                type.NameAr,
                type.DefaultDaysPerYear
            })
            .ToListAsync(cancellationToken);

        var reserved = await dbContext.LeaveRequests
            .ForRead()
            .Where(request =>
                request.EmployeeId == employeeId &&
                (request.Status == "Approved" || request.Status == "Pending") &&
                request.StartDate <= yearEnd &&
                request.EndDate >= yearStart)
            .Select(request => new
            {
                request.LeaveTypeId,
                request.Status,
                Start = request.StartDate < yearStart ? yearStart : request.StartDate,
                End = request.EndDate > yearEnd ? yearEnd : request.EndDate
            })
            .ToListAsync(cancellationToken);

        return types.Select(type =>
        {
            var used = reserved
                .Where(request => request.LeaveTypeId == type.LeaveTypeId && request.Status == "Approved")
                .Sum(request => request.End.DayNumber - request.Start.DayNumber + 1);
            var pending = reserved
                .Where(request => request.LeaveTypeId == type.LeaveTypeId && request.Status == "Pending")
                .Sum(request => request.End.DayNumber - request.Start.DayNumber + 1);
            return new LeaveBalanceDto(
                type.LeaveTypeId,
                type.Name,
                type.NameAr,
                type.DefaultDaysPerYear,
                used,
                Math.Max(0, type.DefaultDaysPerYear - used - pending), pending);
        }).ToList();
    }

    public async Task<LeaveOperationResult> CreateLeaveTypeAsync(
        LeaveTypeInput input,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidTypeInput(input))
        {
            return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business019"));
        }

        var type = new LeaveType(
            input.Name,
            input.NameAr,
            input.DefaultDaysPerYear,
            input.IsPaid,
            input.IsActive);
        dbContext.LeaveTypes.Add(type);
        return await SaveTypeAsync(type, null, cancellationToken);
    }

    public async Task<LeaveOperationResult> UpdateLeaveTypeAsync(
        int id,
        LeaveTypeInput input,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidTypeInput(input))
        {
            return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business019"));
        }

        var type = await dbContext.LeaveTypes
            .SingleOrDefaultAsync(candidate => candidate.LeaveTypeId == id, cancellationToken);
        if (type is null)
        {
            return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business060"));
        }

        var previousName = type.Name;
        var previousNameAr = type.NameAr;
        var previousDays = type.DefaultDaysPerYear;
        var previousPaid = type.IsPaid;
        var previousActive = type.IsActive;
        type.Update(input.Name, input.NameAr, input.DefaultDaysPerYear, input.IsPaid, input.IsActive);
        return await SaveTypeAsync(type, [
            new("Name", previousName, type.Name),
            new("NameAr", previousNameAr, type.NameAr),
            new("DefaultDaysPerYear", previousDays.ToString(CultureInfo.InvariantCulture),
                type.DefaultDaysPerYear.ToString(CultureInfo.InvariantCulture)),
            new("IsPaid", previousPaid.ToString(), type.IsPaid.ToString()),
            new("IsActive", previousActive.ToString(), type.IsActive.ToString())
        ], cancellationToken);
    }

    private async Task<LeaveOperationResult> ReviewAsync(
        int id,
        string reviewerUserId,
        string? notes,
        bool approve,
        CancellationToken cancellationToken)
    {
        var request = await dbContext.LeaveRequests
            .SingleOrDefaultAsync(item => item.LeaveRequestId == id, cancellationToken);
        if (request is null)
        {
            return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business043"));
        }

        if (reviewerUserId != access.UserId || !await access.CanAccessEmployeeAsync(request.EmployeeId, true, cancellationToken))
        {
            await auditLogService.RecordAsync(new AuditLogWriteRequest("Authorization.LeaveReviewDenied", "LeaveRequest", id.ToString(), [], "Failure"), cancellationToken);
            return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Extra000"));
        }

        var previousStatus = request.Status;
        try
        {
            if (approve)
            {
                if (notes?.Length > 1000)
                {
                    return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business058"));
                }
                request.Approve(reviewerUserId, notes);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(notes) || notes.Length > 1000)
                {
                    return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business041"));
                }
                request.Reject(reviewerUserId, notes);
            }
        }
        catch (ArgumentException)
        {
            return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business042"));
        }
        catch (InvalidOperationException)
        {
            return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business039"));
        }

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogService.RecordAsync(new AuditLogWriteRequest(
                approve ? "Leave.Approved" : "Leave.Rejected",
                "LeaveRequest",
                id.ToString(CultureInfo.InvariantCulture),
                [
                    new("Status", previousStatus, request.Status),
                    new("ReviewedAtUtc", null, request.ReviewedAtUtc?.ToString("O", CultureInfo.InvariantCulture))
                ]), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return LeaveOperationResult.Success(id);
        }
        catch (DataConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business034"));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to review leave request {LeaveRequestId}.", id);
            throw;
        }
    }

    private async Task<LeaveOperationResult> SaveTypeAsync(
        LeaveType type,
        IReadOnlyList<AuditChange>? changes,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLogService.RecordAsync(new AuditLogWriteRequest(
                changes is null ? "LeaveType.Created" : "LeaveType.Updated",
                "LeaveType",
                type.LeaveTypeId.ToString(CultureInfo.InvariantCulture),
                changes ?? [
                    new("Name", null, type.Name),
                    new("NameAr", null, type.NameAr),
                    new("DefaultDaysPerYear", null, type.DefaultDaysPerYear.ToString(CultureInfo.InvariantCulture)),
                    new("IsPaid", null, type.IsPaid.ToString()),
                    new("IsActive", null, type.IsActive.ToString())
                ]), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return LeaveOperationResult.Success(type.LeaveTypeId);
        }
        catch (DataWriteException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            if (!exception.IsUnique)
            {
                throw;
            }
            logger.LogWarning(exception, "Leave type violates a database constraint.");
            return LeaveOperationResult.Failure(HRMS.Application.Common.Messages.Get("Business024"));
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(exception, "Failed to save leave type {LeaveTypeId}.", type.LeaveTypeId);
            throw;
        }
    }

    private static bool IsValidTypeInput(LeaveTypeInput input) =>
        !string.IsNullOrWhiteSpace(input.Name) &&
        input.Name.Trim().Length <= 100 &&
        !string.IsNullOrWhiteSpace(input.NameAr) &&
        input.NameAr.Trim().Length <= 100 &&
        input.DefaultDaysPerYear is >= 0 and <= 366;

    private static IQueryable<LeaveRequest> ApplyFilter(
        IQueryable<LeaveRequest> query,
        LeaveFilter filter)
    {
        if (filter.EmployeeId is int employeeId)
        {
            query = query.Where(request => request.EmployeeId == employeeId);
        }
        if (filter.DepartmentId is int departmentId)
        {
            query = query.Where(request => request.Employee!.DepartmentId == departmentId);
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
                throw new ArgumentOutOfRangeException(nameof(filter), "Leave filter year must be between 2000 and 2100.");
            }
            var yearStart = new DateOnly(year, 1, 1);
            var yearEnd = new DateOnly(year, 12, 31);
            query = query.Where(request => request.StartDate <= yearEnd && request.EndDate >= yearStart);
        }
        return query.Where(request => !request.Employee!.IsDeleted);
    }

    private System.Linq.Expressions.Expression<Func<LeaveRequest, LeaveRequestDto>> ToDto() =>
        request => new LeaveRequestDto(
            request.LeaveRequestId,
            request.EmployeeId,
            request.Employee!.EmployeeNumber,
            request.Employee.FirstName + " " + request.Employee.LastName,
            request.Employee.DepartmentId,
            request.Employee.Department!.Name,
            request.LeaveTypeId,
            request.LeaveType!.Name,
            request.LeaveType.NameAr,
            request.StartDate,
            request.EndDate,
            request.TotalDays,
            request.Reason,
            request.Status,
            request.AppliedAtUtc,
            request.ReviewedByUserId,
            dbContext.Users.Where(user => user.Id == request.ReviewedByUserId)
                .Select(user => user.UserName)
                .FirstOrDefault(),
            request.ReviewedAtUtc,
            request.ReviewNotes, request.StartTime, request.EndTime);
}
