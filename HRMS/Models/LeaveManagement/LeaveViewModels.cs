using System.ComponentModel.DataAnnotations;
using HRMS.Application.LeaveManagement;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRMS.Models.LeaveManagement;

public sealed class LeaveListViewModel
{
    public string? Status { get; set; }

    public int? Year { get; set; }

    public IReadOnlyList<LeaveRequestDto> Requests { get; set; } = Array.Empty<LeaveRequestDto>();

    public IReadOnlyList<LeaveBalanceDto> Balances { get; set; } = Array.Empty<LeaveBalanceDto>();
}

public sealed class LeaveRequestFormViewModel
{
    [DataType(DataType.Time), Display(Name = "LeaveStartTime")]
    public TimeOnly? StartTime { get; set; }

    [DataType(DataType.Time), Display(Name = "LeaveEndTime")]
    public TimeOnly? EndTime { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "LeaveTypeRequired")]
    [Display(Name = "Label.LeaveType")]
    public int LeaveTypeId { get; set; }

    [Required(ErrorMessage = "LeaveStartRequired")]
    [DataType(DataType.Date)]
    [Display(Name = "Label.LeaveStart")]
    public DateOnly? StartDate { get; set; }

    [Required(ErrorMessage = "LeaveEndRequired")]
    [DataType(DataType.Date)]
    [Display(Name = "Label.LeaveEnd")]
    public DateOnly? EndDate { get; set; }

    [Required(ErrorMessage = "LeaveReasonRequired")]
    [StringLength(1000)]
    [Display(Name = "Label.Reason")]
    public string Reason { get; set; } = string.Empty;

    public IReadOnlyList<SelectListItem> LeaveTypes { get; set; } = Array.Empty<SelectListItem>();
}

public sealed class LeaveDecisionViewModel
{
    public int LeaveRequestId { get; set; }

    [StringLength(1000)]
    [Display(Name = "Label.ReviewNotes")]
    public string? Notes { get; set; }
}

public sealed class LeaveTypeFormViewModel
{
    public int? LeaveTypeId { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Label.LeaveTypeName")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Display(Name = "Label.LeaveTypeNameAr")]
    public string NameAr { get; set; } = string.Empty;

    [Range(0, 366)]
    [Display(Name = "Label.DefaultDays")]
    public int DefaultDaysPerYear { get; set; }

    [Display(Name = "Label.IsPaid")]
    public bool IsPaid { get; set; } = true;

    [Display(Name = "Label.IsActive")]
    public bool IsActive { get; set; } = true;

    public IReadOnlyList<HRMS.Application.LeaveManagement.LeaveTypeDto> Types { get; set; } =
        Array.Empty<HRMS.Application.LeaveManagement.LeaveTypeDto>();
}
