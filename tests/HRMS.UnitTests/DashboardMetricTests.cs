using HRMS.Application.Reporting;
using HRMS.Domain.Employees;

namespace HRMS.UnitTests;

public class DashboardMetricTests
{
    private static IQueryable<Employee> Employees()
    {
        Employee Make(string number, string status, int department, DateOnly hire) =>
            new(number, "Test", "Employee", number + "@example.test", hire, status, department, 1, "FullTime");
        var deleted = Make("DELETED", "Active", 1, new(2026, 1, 15));
        deleted.SoftDelete(DateTime.UtcNow, null);
        return new[] {
            Make("START", "Active", 1, new(2026, 1, 1)),
            Make("END", "Inactive", 1, new(2026, 1, 31)),
            Make("OLD", "نشط", 1, new(2025, 12, 31)),
            Make("OTHER", "Active", 2, new(2026, 1, 15)), deleted
        }.AsQueryable();
    }

    [Fact] public void ActiveHeadcountExcludesDeletedInactiveAndOtherDepartments() =>
        Assert.Equal(2, DashboardMetrics.ActiveEmployees(Employees(), 1).Count());

    [Fact] public void NewHiresIncludeBothDateBoundariesAndInactiveButExcludeDeletedAndOtherDepartments() =>
        Assert.Equal(2, DashboardMetrics.NewHires(Employees(), 1, new(2026, 1, 1), new(2026, 1, 31)).Count());

    [Fact] public void StatusGroupsSumToScopedUndeletedHeadcount()
    {
        var counts = DashboardMetrics.StatusCounts(Employees(), 1).ToList();
        Assert.Equal(3, counts.Sum(g => g.Count));
        Assert.Equal(1, counts.Single(g => g.Status == "Inactive").Count);
        Assert.Equal(1, counts.Single(g => g.Status == "نشط").Count);
    }

    [Fact] public void ReversedDashboardPeriodIsRejected() =>
        Assert.Throws<ArgumentException>(() => DashboardMetrics.NewHires(Employees(), null, new(2026, 2, 1), new(2026, 1, 1)));
}
