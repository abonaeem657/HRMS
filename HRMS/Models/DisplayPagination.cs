using Microsoft.AspNetCore.WebUtilities;

namespace HRMS.Models;

public sealed record DisplayPagination(int TotalCount, int Page, bool ShowAll, string Prefix = "")
{
    public const int PageSize = 6;
    public int TotalPages => ShowAll ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public int First => TotalCount == 0 ? 0 : ShowAll ? 1 : (Page - 1) * PageSize + 1;
    public int Last => ShowAll ? TotalCount : Math.Min(Page * PageSize, TotalCount);

    public static bool IsShowAll(HttpRequest request, string prefix = "") =>
        bool.TryParse(request.Query[prefix + "ShowAll"], out var all) && all;

    public static DisplayPagination Create(HttpRequest request, int totalCount, int page = 0, string prefix = "")
    {
        var showAll = IsShowAll(request, prefix);
        if (page <= 0) int.TryParse(request.Query[prefix + "Page"], out page);
        var pages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        return new(totalCount, showAll ? 1 : Math.Clamp(page, 1, pages), showAll, prefix);
    }

    public static HRMS.Application.Common.PageRequest Request(HttpRequest request, string prefix = "")
    {
        int.TryParse(request.Query[prefix + "Page"], out var page);
        return new(Math.Max(1, page), PageSize, IsShowAll(request, prefix));
    }
    public string Link(HttpRequest request, int page, bool showAll)
    {
        var query = request.Query.ToDictionary(pair => pair.Key, pair => (string?)pair.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        query.Remove(Prefix + "Page");
        query.Remove(Prefix + "PageSize");
        query.Remove(Prefix + "ShowAll");
        query[Prefix + "Page"] = Math.Clamp(page, 1, TotalPages).ToString(System.Globalization.CultureInfo.InvariantCulture);
        query[Prefix + "ShowAll"] = showAll ? "true" : "false";
        return QueryHelpers.AddQueryString(request.PathBase.Add(request.Path).Value!, query);
    }
}
