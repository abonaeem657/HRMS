using HRMS.Infrastructure.Attendance;
using Microsoft.Extensions.Configuration;

namespace HRMS.UnitTests;

public class BusinessClockTests
{
    private readonly BusinessClock clock = new(new ConfigurationBuilder().Build());

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    public void JordanDisplayMovesPastMidnightEvenForSqlUnspecifiedKind(DateTimeKind kind)
    {
        var utc = new DateTime(2026, 10, 14, 22, 30, 0, kind);
        Assert.Equal(new DateTime(2026, 10, 15, 1, 30, 0), clock.ToLocal(utc));
    }

    [Fact]
    public void MissingAttendanceTimeRemainsMissing() => Assert.Null(clock.ToLocal(null));

    [Fact]
    public void JordanYearBoundaryMatchesUtcQueryBoundary()
    {
        var start = clock.StartOfDayUtc(new DateOnly(2027, 1, 1));
        Assert.Equal(new DateTime(2026, 12, 31, 21, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2027, 1, 1), clock.ToLocal(start));
    }
}
