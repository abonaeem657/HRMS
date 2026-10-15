using System.Globalization;
using System.Resources;
namespace HRMS.Application.Common;
public static class Messages
{
    private static readonly ResourceManager Resources = new("HRMS.Application.Common.Messages", typeof(Messages).Assembly);
    public static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
