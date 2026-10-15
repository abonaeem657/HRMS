namespace HRMS.Application.Common;

public sealed record PageRequest(int Page = 1, int PageSize = 6, bool ShowAll = false);
public sealed record PageResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
