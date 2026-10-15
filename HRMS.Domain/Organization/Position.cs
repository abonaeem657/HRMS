using HRMS.Domain.Employees;

namespace HRMS.Domain.Organization;

public class Position
{
    private Position()
    {
    }

    public Position(string code, string title, bool isActive, int? departmentId = null,
        string? description = null, string? grade = null)
    {
        Code = RequireValue(code, nameof(code));
        Title = RequireValue(title, nameof(title));
        DepartmentId = departmentId;
        Description = NormalizeOptional(description);
        Grade = NormalizeOptional(grade);
        IsActive = isActive;
    }

    public int PositionId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public int? DepartmentId { get; private set; }

    public string? Description { get; private set; }

    public string? Grade { get; private set; }

    public bool IsActive { get; private set; }

    public Department? Department { get; private set; }

    public ICollection<Employee> Employees { get; } = new List<Employee>();

    public void Update(string code, string title, int? departmentId,
        string? description = null, string? grade = null)
    {
        var validatedCode = RequireValue(code, nameof(code));
        var validatedTitle = RequireValue(title, nameof(title));

        Code = validatedCode;
        Title = validatedTitle;
        DepartmentId = departmentId;
        Description = NormalizeOptional(description);
        Grade = NormalizeOptional(grade);
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

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
