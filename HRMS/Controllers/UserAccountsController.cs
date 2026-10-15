using HRMS.Models;
using HRMS.Application.Identity;
using HRMS.Models.Identity;
using HRMS.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRMS.Controllers;

[Authorize(Roles = IdentityRoles.SystemAdministrator)]
public sealed class UserAccountsController(
    IUserAccountService userAccountService,
    IStringLocalizer<SharedResource> localizer) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var roleNames = IdentityRoles.All.ToDictionary(
            role => role.Name,
            role => localizer[role.DisplayName].Value);

        var result = await userAccountService.SearchAccountsAsync(DisplayPagination.Request(Request), cancellationToken);
        ViewData["Pagination"] = DisplayPagination.Create(Request, result.TotalCount, result.Page);

        return View(new UserAccountListViewModel
        {
            Accounts = result.Items.Select(account => new UserAccountRowViewModel(
                account.UserId,
                account.Email,
                roleNames.GetValueOrDefault(account.RoleName, account.RoleName),
                account.IsActive,
                account.EmployeeId,
                account.EmployeeNumber,
                account.EmployeeName)).ToList()
        });
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new UserAccountCreateViewModel();
        await PopulateCreateOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        UserAccountCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCreateOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var result = await userAccountService.CreateAsync(
            model.Email, model.Password, model.RoleName, model.EmployeeId, cancellationToken);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            await PopulateCreateOptionsAsync(model, cancellationToken);
            return View(model);
        }

        TempData["StatusMessage"] = localizer["UserCreated"].Value;
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        var account = await userAccountService.GetAccountAsync(id, cancellationToken);

        if (account is null)
            return NotFound();

        var model = new UserAccountEditViewModel
        {
            UserId = account.UserId,
            Email = account.Email,
            RoleName = account.RoleName,
            EmployeeId = account.EmployeeId
        };

        await PopulateEditOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        UserAccountEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.UserId)
            return BadRequest();

        if (!ModelState.IsValid)
        {
            await PopulateEditOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var result = await userAccountService.UpdateAsync(
            id, model.Email, model.RoleName, model.EmployeeId, cancellationToken);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            await PopulateEditOptionsAsync(model, cancellationToken);
            return View(model);
        }

        TempData["StatusMessage"] = localizer["UserUpdated"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(
        string id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(id))
            return BadRequest();

        var result = await userAccountService.SetActiveAsync(id, isActive, cancellationToken);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join(
                " ", result.Errors.Select(error => error.Message));
        }
        else
        {
            TempData["StatusMessage"] = isActive
                ? localizer["UserEnabled"].Value
                : localizer["UserDisabled"].Value;
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> ResetPassword(
        string id,
        CancellationToken cancellationToken)
    {
        var account = await userAccountService.GetAccountAsync(id, cancellationToken);

        return account is null
            ? NotFound()
            : View(new ResetPasswordViewModel
            {
                UserId = account.UserId,
                Email = account.Email
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await userAccountService.ResetPasswordAsync(
            model.UserId, model.NewPassword, cancellationToken);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            return View(model);
        }

        TempData["StatusMessage"] = localizer["UserPasswordReset"].Value;
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateCreateOptionsAsync(
        UserAccountCreateViewModel model,
        CancellationToken cancellationToken)
    {
        model.Roles = IdentityRoles.All
            .Select(role => new SelectListItem(
                localizer[role.DisplayName].Value,
                role.Name,
                role.Name == model.RoleName))
            .ToList();

        var employees = await userAccountService.GetAvailableEmployeesAsync(
            includeEmployeeId: model.EmployeeId,
            cancellationToken: cancellationToken);

        model.Employees = employees.Select(employee => new SelectListItem(
            $"{employee.EmployeeNumber} - {employee.FullName}",
            employee.EmployeeId.ToString(),
            employee.EmployeeId == model.EmployeeId)).ToList();
    }

    private async Task PopulateEditOptionsAsync(
        UserAccountEditViewModel model,
        CancellationToken cancellationToken)
    {
        model.Roles = IdentityRoles.All
            .Select(role => new SelectListItem(
                localizer[role.DisplayName].Value,
                role.Name,
                role.Name == model.RoleName))
            .ToList();

        var employees = await userAccountService.GetAvailableEmployeesAsync(
            model.UserId,
            model.EmployeeId,
            cancellationToken);

        model.Employees = employees.Select(employee => new SelectListItem(
            $"{employee.EmployeeNumber} - {employee.FullName}",
            employee.EmployeeId.ToString(),
            employee.EmployeeId == model.EmployeeId)).ToList();
    }

    private void AddErrors(IEnumerable<UserAccountError> errors)
    {
        foreach (var error in errors)
            ModelState.AddModelError(error.Key, error.Message);
    }
}
