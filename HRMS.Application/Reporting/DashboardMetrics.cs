using HRMS.Domain.Employees;

namespace HRMS.Application.Reporting;

// Shared metric definitions remain IQueryable so the database performs the aggregation.
public static class DashboardMetrics
{
    public static IQueryable<Employee> InScope(IQueryable<Employee> employees, int? departmentId) =>
        employees.Where(e => !e.IsDeleted && (departmentId == null || e.DepartmentId == departmentId));

    public static IQueryable<Employee> ActiveEmployees(IQueryable<Employee> employees, int? departmentId) =>
        InScope(employees, departmentId).Where(e => e.EmploymentStatus == "Active" ||
            e.EmploymentStatus == "نشط" || e.EmploymentStatus == "على رأس العمل");

    public static IQueryable<Employee> NewHires(IQueryable<Employee> employees, int? departmentId, DateOnly from, DateOnly to)
    {
        if (to < from) throw new ArgumentException("Dashboard end date cannot be before its start date.");
        return InScope(employees, departmentId).Where(e => e.HireDate >= from && e.HireDate <= to);
    }

    public static IQueryable<StatusHeadcountItem> StatusCounts(IQueryable<Employee> employees, int? departmentId) =>
        InScope(employees, departmentId).GroupBy(e => e.EmploymentStatus).OrderBy(group => group.Key)
            .Select(group => new StatusHeadcountItem(group.Key, group.Count()));
}
