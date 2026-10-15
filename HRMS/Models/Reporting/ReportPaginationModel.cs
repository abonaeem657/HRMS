namespace HRMS.Models.Reporting;

public sealed record ReportPaginationModel(
    string Action,
    int Page,
    int TotalPages,
    int? DepartmentId,
    int? PositionId,
    string? Status,
    DateOnly? HiredAfter,
    DateOnly? HiredBefore,
    int? EmployeeId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int? LeaveTypeId,
    int? Year,
    string? SortBy = null,
    bool SortDescending = false,
    int PageSize = 25);
