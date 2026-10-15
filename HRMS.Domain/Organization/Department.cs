using HRMS.Domain.Employees;

namespace HRMS.Domain.Organization;

public class Department
{
    private Department()
    {
    }

    public Department(string code, string name, bool isActive)
    {
        Code = RequireValue(code, nameof(code));
        Name = RequireValue(name, nameof(name));
        IsActive = isActive;
    }

    public int DepartmentId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public int? ManagerEmployeeId { get; private set; }

    public bool IsActive { get; private set; }

    public Employee? Manager { get; private set; }

    public ICollection<Employee> Employees { get; } = new List<Employee>();

    public void Update(string code, string name)
    {
        var validatedCode = RequireValue(code, nameof(code));
        var validatedName = RequireValue(name, nameof(name));

        Code = validatedCode;
        Name = validatedName;
    }

    public void AssignManager(int? managerEmployeeId)
    {
        ManagerEmployeeId = managerEmployeeId;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

    private static string RequireValue(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}
