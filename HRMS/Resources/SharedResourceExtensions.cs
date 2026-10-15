using Microsoft.Extensions.Localization;

namespace HRMS.Resources;

public static class SharedResourceExtensions
{
    // Employment fields accept custom values, which need not have a resource entry.
    public static string LocalizeValue(this IStringLocalizer<SharedResource> localizer, string prefix, string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var text = localizer[prefix + value];
        return text.ResourceNotFound ? value : text.Value;
    }
}
