namespace HRMS.Domain.LeaveManagement;

public class LeaveType
{
    private LeaveType()
    {
    }

    public LeaveType(
        string name,
        string nameAr,
        int defaultDaysPerYear,
        bool isPaid = true,
        bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        if (defaultDaysPerYear < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultDaysPerYear), "Default days per year cannot be negative.");
        }

        Name = name.Trim();
        NameAr = nameAr.Trim();
        DefaultDaysPerYear = defaultDaysPerYear;
        IsPaid = isPaid;
        IsActive = isActive;
    }

    public int LeaveTypeId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NameAr { get; private set; } = string.Empty;

    public int DefaultDaysPerYear { get; private set; }

    public bool IsPaid { get; private set; }

    public bool IsActive { get; private set; } = true;

    public void Update(
        string name,
        string nameAr,
        int defaultDaysPerYear,
        bool isPaid,
        bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        if (defaultDaysPerYear < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultDaysPerYear), "Default days per year cannot be negative.");
        }

        Name = name.Trim();
        NameAr = nameAr.Trim();
        DefaultDaysPerYear = defaultDaysPerYear;
        IsPaid = isPaid;
        IsActive = isActive;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}

