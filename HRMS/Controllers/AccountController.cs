using HRMS.Application.Identity;
using HRMS.Application.Auditing;
using HRMS.Infrastructure.Identity;
using HRMS.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using HRMS.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.RateLimiting;

namespace HRMS.Controllers;

public sealed class AccountController(
    SignInManager<HRMSUser> signInManager,
    UserManager<HRMSUser> userManager,
    IUserAccountService userAccountService,
    IAuditLogService auditLogService,
    IConfiguration configuration, IStringLocalizer<SharedResource> T) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl,
            SetupAvailable = IsInitialSetupEnabled() &&
                !await userAccountService.AnyAccountsAsync(cancellationToken)
        });
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        CancellationToken cancellationToken)
    {
        model.SetupAvailable = IsInitialSetupEnabled() &&
            !await userAccountService.AnyAccountsAsync(cancellationToken);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null || !user.IsActive ||
            !await userAccountService.CanSignInAsync(user.Id, cancellationToken))
        {
            await auditLogService.RecordAsync(new AuditLogWriteRequest(
                "Authentication.LoginRejected",
                "UserAccount",
                user?.Id,
                [],
                "Failure"), cancellationToken);
            ModelState.AddModelError(string.Empty, T["SignInFailed"].Value);
            return View(model);
        }

        var result = await signInManager.CheckPasswordSignInAsync(
            user,
            model.Password,
            lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            await auditLogService.RecordAsync(new AuditLogWriteRequest(
                result.IsLockedOut
                    ? "Authentication.LoginLockedOut"
                    : "Authentication.LoginFailed",
                "UserAccount",
                user.Id,
                [],
                "Failure"), cancellationToken);
            ModelState.AddModelError(
                string.Empty,
                result.IsLockedOut
                    ? T["SignInTemporarilyLocked"].Value
                    : T["SignInFailed"].Value);
            return View(model);
        }

        await signInManager.SignInAsync(user, model.RememberMe);
        await auditLogService.RecordAsync(new AuditLogWriteRequest(
            "Authentication.LoginSucceeded",
            "UserAccount",
            user.Id,
            [],
            ActorUserId: user.Id), cancellationToken);
        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is not null)
        {
            await auditLogService.RecordAsync(new AuditLogWriteRequest(
                "Authentication.Logout",
                "UserAccount",
                userId,
                [],
                ActorUserId: userId));
        }

        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Setup(CancellationToken cancellationToken)
    {
        if (!IsInitialSetupEnabled() ||
            await userAccountService.AnyAccountsAsync(cancellationToken))
        {
            return RedirectToAction(nameof(Login));
        }

        return View(new InitialAdministratorViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Setup(
        InitialAdministratorViewModel model,
        CancellationToken cancellationToken)
    {
        if (!IsInitialSetupEnabled() ||
            await userAccountService.AnyAccountsAsync(cancellationToken))
        {
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await userAccountService.CreateFirstAdministratorAsync(
            model.Email,
            model.Password,
            cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            return View(model);
        }

        var user = await userManager.FindByIdAsync(result.UserId!);
        if (user is null)
        {
            throw new InvalidOperationException("The initial administrator was not found after creation.");
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [Authorize]
    public IActionResult AccessDenied() => View();

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        var result = await userManager.ChangePasswordAsync(
            user,
            model.CurrentPassword,
            model.NewPassword);
        if (!result.Succeeded)
        {
            AddErrors(result.Errors.Select(error =>
                new UserAccountError(
                    error.Code.Contains("Password", StringComparison.OrdinalIgnoreCase)
                        ? nameof(model.NewPassword)
                        : string.Empty,
                    error.Code == "PasswordMismatch"
                        ? T["CurrentPasswordInvalid"].Value
                        : error.Description)));
            return View(model);
        }

        await auditLogService.RecordAsync(new AuditLogWriteRequest(
            "UserAccount.PasswordChanged",
            "UserAccount",
            user.Id,
            []), cancellationToken);
        await signInManager.RefreshSignInAsync(user);
        TempData["StatusMessage"] = T["PasswordChanged"].Value;
        return RedirectToAction(nameof(ChangePassword));
    }

    private void AddErrors(IEnumerable<UserAccountError> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(error.Key, error.Message);
        }
    }

    private bool IsInitialSetupEnabled() =>
        configuration.GetValue<bool>("Identity:AllowInitialAdministratorSetup");
}
