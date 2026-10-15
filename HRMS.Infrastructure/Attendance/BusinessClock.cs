using HRMS.Application.Common;
using Microsoft.Extensions.Configuration;
namespace HRMS.Infrastructure.Attendance;
public sealed class BusinessClock(IConfiguration configuration) : IBusinessClock
{
    private readonly TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(configuration["Attendance:BusinessTimeZoneId"] ?? "Jordan Standard Time");
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
    // SQL datetime values may return with Unspecified kind; these columns store UTC.
    public DateTime? ToLocal(DateTime? utc) => utc.HasValue
        ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), zone)
        : null;
    public DateTime StartOfDayUtc(DateOnly date) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);
}
