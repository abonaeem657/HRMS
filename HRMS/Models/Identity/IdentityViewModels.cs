using System.ComponentModel.DataAnnotations;
using HRMS.Application.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRMS.Models.Identity;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "EmailRequired")]
    [EmailAddress(ErrorMessage = "ValidEmailRequired")]
    [Display(Name = "Label.Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "PasswordRequired")]
    [DataType(DataType.Password)]
    [Display(Name = "Label.Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Label.RememberMe")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }

    public bool SetupAvailable { get; set; }
}

public sealed class InitialAdministratorViewModel
{
    [Required(ErrorMessage = "EmailRequired")]
    [EmailAddress(ErrorMessage = "ValidEmailRequired")]
    [Display(Name = "Label.Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "PasswordRequired")]
    [DataType(DataType.Password)]
    [Display(Name = "Label.Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "PasswordConfirmationRequired")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "PasswordsDoNotMatch")]
    [Display(Name = "Label.ConfirmPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessage = "CurrentPasswordRequired")]
    [DataType(DataType.Password)]
    [Display(Name = "Label.CurrentPassword")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "NewPasswordRequired")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 10, ErrorMessage = "PasswordMinimumLength")]
    [Display(Name = "Label.NewPassword")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "PasswordConfirmationRequired")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "PasswordsDoNotMatch")]
    [Display(Name = "Label.ConfirmPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class UserAccountListViewModel
{
    public IReadOnlyList<UserAccountRowViewModel> Accounts { get; set; } =
        Array.Empty<UserAccountRowViewModel>();
}

public sealed record UserAccountRowViewModel(
    string UserId,
    string Email,
    string RoleDisplayName,
    bool IsActive,
    int? EmployeeId,
    string? EmployeeNumber,
    string? EmployeeName);

public sealed class UserAccountCreateViewModel
{
    [Required(ErrorMessage = "EmailRequired")]
    [EmailAddress(ErrorMessage = "ValidEmailRequired")]
    [Display(Name = "Label.Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "TemporaryPasswordRequired")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 10, ErrorMessage = "PasswordMinimumLength")]
    [Display(Name = "Label.Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "RoleRequired")]
    [Display(Name = "Label.Role")]
    public string RoleName { get; set; } = IdentityRoles.HROfficer;

    [Display(Name = "Label.LinkedEmployee")]
    public int? EmployeeId { get; set; }

    public IReadOnlyList<SelectListItem> Roles { get; set; } =
        Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> Employees { get; set; } =
        Array.Empty<SelectListItem>();
}

public sealed class UserAccountEditViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "EmailRequired")]
    [EmailAddress(ErrorMessage = "ValidEmailRequired")]
    [Display(Name = "Label.Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "RoleRequired")]
    [Display(Name = "Label.Role")]
    public string RoleName { get; set; } = IdentityRoles.HROfficer;

    [Display(Name = "Label.LinkedEmployee")]
    public int? EmployeeId { get; set; }

    public IReadOnlyList<SelectListItem> Roles { get; set; } =
        Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> Employees { get; set; } =
        Array.Empty<SelectListItem>();
}

public sealed class ResetPasswordViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "NewPasswordRequired")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 10, ErrorMessage = "PasswordMinimumLength")]
    [Display(Name = "Label.NewPassword")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "PasswordConfirmationRequired")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "PasswordsDoNotMatch")]
    [Display(Name = "Label.ConfirmPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
