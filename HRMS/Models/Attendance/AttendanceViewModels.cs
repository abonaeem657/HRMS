using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRMS.Models.Attendance;

public sealed class AttendanceListViewModel
{
    public int? EmployeeId { get; set; }

    public int? DepartmentId { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? FromDate { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? ToDate { get; set; }

    public string? Status { get; set; }

    public IReadOnlyList<HRMS.Application.Attendance.AttendanceRecordDto> Records { get; set; } =
        Array.Empty<HRMS.Application.Attendance.AttendanceRecordDto>();

    public HRMS.Application.Attendance.AttendanceSummaryDto Summary { get; set; } =
        new(0, 0, 0, 0, 0, 0);

    public IReadOnlyList<SelectListItem> Employees { get; set; } =
        Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> Departments { get; set; } =
        Array.Empty<SelectListItem>();

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public int TotalCount { get; set; }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed class AttendanceCorrectionViewModel
{
    public int? AttendanceId { get; set; }
    public byte[]? RowVersion { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? CheckOutDate { get; set; }
    public bool CheckOutNextDay { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "EmployeeRequired")]
    [Display(Name = "Employee")]
    public int EmployeeId { get; set; }

    [Required(ErrorMessage = "AttendanceDateRequired")]
    [DataType(DataType.Date)]
    [Display(Name = "Label.AttendanceDate")]
    public DateOnly? WorkDate { get; set; }

    [Display(Name = "Label.CheckInLocal")]
    public string? CheckInLocal { get; set; }

    [Display(Name = "Label.CheckOutLocal")]
    public string? CheckOutLocal { get; set; }

    [Required]
    [RegularExpression("Present|Late|Absent|HalfDay")]
    [Display(Name = "Label.Status")]
    public string Status { get; set; } = "Present";

    [StringLength(1000)]
    [Display(Name = "Label.Notes")]
    public string? Notes { get; set; }

    public IReadOnlyList<SelectListItem> Employees { get; set; } =
        Array.Empty<SelectListItem>();
}
