using HRMS.Models;
using HRMS.Application.Employees;
using HRMS.Application.Identity;
using HRMS.Models.Employees;
using HRMS.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRMS.Controllers;

[Authorize]
public sealed class EmployeesController(IEmployeeService employeeService, IStringLocalizer<SharedResource> T) : Controller
{
    [HttpGet]
    [Authorize(Roles = IdentityRoles.ManageEmployees)]
    public async Task<IActionResult> Deleted(int page = 1, int pageSize = 6, CancellationToken cancellationToken = default)
    {
        var result = await employeeService.GetDeletedAsync(page, DisplayPagination.PageSize, cancellationToken, DisplayPagination.IsShowAll(Request));
        ViewData["Pagination"] = DisplayPagination.Create(Request, result.TotalCount, result.Page);
        return View(result);
    }

    [HttpPost]
    [Authorize(Roles = IdentityRoles.ManageEmployees)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, CancellationToken cancellationToken)
    {
        var actorUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!await employeeService.RestoreAsync(id, actorUserId, cancellationToken))
        {
            TempData["StatusMessage"] = T["EmployeeRestoreNotFound"].Value;
        }
        else
        {
            TempData["StatusMessage"] = T["EmployeeRestored"].Value;
        }

        return RedirectToAction(nameof(Deleted));
    }

    [Authorize(Roles = IdentityRoles.ReadHrData)]
    public async Task<IActionResult> Index(
        [FromQuery] EmployeeListViewModel query,
        CancellationToken cancellationToken)
    {
        var result = await employeeService.SearchAsync(
            new EmployeeListQuery(
                query.Search,
                query.DepartmentId,
                query.EmploymentStatus,
                query.SortBy,
                query.SortDescending,
                query.Page,
                DisplayPagination.PageSize, DisplayPagination.IsShowAll(Request)),
            cancellationToken);
        var options = await employeeService.GetListFilterOptionsAsync(cancellationToken);

        query.Employees = result.Items
            .Select(employee => new EmployeeListRowViewModel(
                employee.EmployeeId,
                employee.EmployeeNumber,
                employee.FullName,
                employee.WorkEmail,
                employee.DepartmentName,
                employee.PositionTitle,
                employee.EmploymentStatus,
                employee.HireDate))
            .ToList();
        query.TotalCount = result.TotalCount;
        ViewData["Pagination"] = DisplayPagination.Create(Request, result.TotalCount, result.Page);
        query.Page = result.Page;
        query.PageSize = result.PageSize;
        query.Departments = options.Departments
            .Select(department => new SelectListItem(
                department.Text,
                department.Id.ToString(),
                department.Id == query.DepartmentId))
            .ToList();
        query.EmploymentStatuses = options.EmploymentStatuses;

        return IsAjaxRequest()
            ? PartialView("_Results", query)
            : View(query);
    }

    [Authorize(Roles = IdentityRoles.ReadHrData)]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var employee = await employeeService.GetByIdAsync(id, cancellationToken);
        if (employee is null)
        {
            return NotFound();
        }

        var model = ToDetailsViewModel(employee);
        return IsModalRequest()
            ? PartialView("_DetailsModal", model)
            : View(model);
    }

    [HttpGet]
    [Authorize(Roles = IdentityRoles.ManageEmployees)]
    public async Task<IActionResult> PositionsForDepartment(
        int departmentId,
        CancellationToken cancellationToken)
    {
        var options = await employeeService.GetFormOptionsAsync(
            departmentId: departmentId,
            cancellationToken: cancellationToken);
        return Json(options.Positions.Select(option => new
        {
            id = option.Id,
            text = option.Text
        }));
    }

    [Authorize(Roles = IdentityRoles.ManageEmployees)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new EmployeeFormViewModel();
        await PopulateOptionsAsync(model, null, cancellationToken);
        return IsModalRequest()
            ? PartialView("_FormModal", model)
            : View(model);
    }

    [HttpPost]
    [Authorize(Roles = IdentityRoles.ManageEmployees)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        EmployeeFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, null, cancellationToken);
            return IsAjaxRequest()
                ? PartialView("_FormModal", model)
                : View(model);
        }

        var result = await employeeService.CreateAsync(ToInput(model), cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            await PopulateOptionsAsync(model, null, cancellationToken);
            return IsAjaxRequest()
                ? PartialView("_FormModal", model)
                : View(model);
        }

        if (IsAjaxRequest())
        {
            return Json(new { message = T["EmployeeCreated"].Value });
        }

        TempData["StatusMessage"] = T["EmployeeCreated"].Value;
        return RedirectToAction(nameof(Details), new { id = result.EmployeeId });
    }

    [Authorize(Roles = IdentityRoles.ManageEmployees)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var employee = await employeeService.GetByIdAsync(id, cancellationToken);
        if (employee is null)
        {
            return NotFound();
        }

        var model = ToFormViewModel(employee);
        await PopulateOptionsAsync(model, id, cancellationToken);
        ViewData["EmployeeId"] = id;
        return IsModalRequest()
            ? PartialView("_FormModal", model)
            : View(model);
    }

    [HttpPost]
    [Authorize(Roles = IdentityRoles.ManageEmployees)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        EmployeeFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, id, cancellationToken);
            ViewData["EmployeeId"] = id;
            return IsAjaxRequest()
                ? PartialView("_FormModal", model)
                : View(model);
        }

        var result = await employeeService.UpdateAsync(id, ToInput(model), cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            await PopulateOptionsAsync(model, id, cancellationToken);
            ViewData["EmployeeId"] = id;
            return IsAjaxRequest()
                ? PartialView("_FormModal", model)
                : View(model);
        }

        if (IsAjaxRequest())
        {
            return Json(new { message = T["EmployeeUpdated"].Value });
        }

        TempData["StatusMessage"] = T["EmployeeUpdated"].Value;
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = IdentityRoles.ManageEmployees)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var employee = await employeeService.GetByIdAsync(id, cancellationToken);
        return employee is null
            ? NotFound()
            : View(ToDetailsViewModel(employee));
    }

    [HttpPost]
    [ActionName(nameof(Delete))]
    [Authorize(Roles = IdentityRoles.ManageEmployees)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(
        int id,
        CancellationToken cancellationToken)
    {
        if (!await employeeService.SoftDeleteAsync(
                id,
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                cancellationToken))
        {
            if (IsAjaxRequest())
            {
                return NotFound(new { message = T["EmployeeRestoreNotFound"].Value });
            }

            return NotFound();
        }

        if (IsAjaxRequest())
        {
            return Json(new
            {
                message = T["EmployeeDisabled"].Value,
                isActive = false
            });
        }

        TempData["StatusMessage"] = T["EmployeeDisabled"].Value;
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateOptionsAsync(
        EmployeeFormViewModel model,
        int? excludeEmployeeId,
        CancellationToken cancellationToken)
    {
        var options = await employeeService.GetFormOptionsAsync(
            excludeEmployeeId,
            model.DepartmentId > 0 ? model.DepartmentId : null,
            cancellationToken);

        model.Departments = options.Departments
            .Select(option => new SelectListItem(
                option.Text,
                option.Id.ToString(),
                option.Id == model.DepartmentId))
            .ToList();
        model.Positions = options.Positions
            .Select(option => new SelectListItem(
                option.Text,
                option.Id.ToString(),
                option.Id == model.PositionId))
            .ToList();
        model.Managers = options.Managers
            .Select(option => new SelectListItem(
                option.Text,
                option.Id.ToString(),
                option.Id == model.ManagerEmployeeId))
            .ToList();
    }

    private void AddErrors(IReadOnlyDictionary<string, string[]> errors)
    {
        foreach (var (key, messages) in errors)
        {
            var modelKey = key switch
            {
                nameof(EmployeeInput.EmployeeNumber) => nameof(EmployeeFormViewModel.EmployeeNumber),
                nameof(EmployeeInput.DepartmentId) => nameof(EmployeeFormViewModel.DepartmentId),
                nameof(EmployeeInput.PositionId) => nameof(EmployeeFormViewModel.PositionId),
                nameof(EmployeeInput.ManagerEmployeeId) => nameof(EmployeeFormViewModel.ManagerEmployeeId),
                _ => string.Empty
            };

            foreach (var message in messages)
            {
                ModelState.AddModelError(modelKey, message);
            }

        }
    }

    private bool IsAjaxRequest() =>
        string.Equals(Request.Headers["X-Requested-With"].ToString(), "XMLHttpRequest", StringComparison.Ordinal);

    private bool IsModalRequest() =>
        bool.TryParse(Request.Query["modal"].ToString(), out var isModal) && isModal;

    private static EmployeeInput ToInput(EmployeeFormViewModel model) =>
        new(
            model.EmployeeNumber,
            model.FirstName,
            model.LastName,
            model.WorkEmail,
            model.HireDate ?? default,
            model.EmploymentStatus,
            model.EmploymentType,
            model.DepartmentId,
            model.PositionId,
            model.ManagerEmployeeId,
            model.PhoneNumber,
            model.WorkLocation, model.RowVersion);

    private static EmployeeFormViewModel ToFormViewModel(EmployeeDetails employee) =>
        new()
        {
            EmployeeNumber = employee.EmployeeNumber,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            WorkEmail = employee.WorkEmail,
            PhoneNumber = employee.PhoneNumber,
            HireDate = employee.HireDate,
            EmploymentStatus = employee.EmploymentStatus,
            EmploymentType = employee.EmploymentType,
            DepartmentId = employee.DepartmentId,
            PositionId = employee.PositionId,
            ManagerEmployeeId = employee.ManagerEmployeeId,
            RowVersion = employee.RowVersion,
            WorkLocation = employee.WorkLocation
        };

    private static EmployeeDetailsViewModel ToDetailsViewModel(EmployeeDetails employee) =>
        new(
            employee.EmployeeId,
            employee.EmployeeNumber,
            employee.FirstName,
            employee.LastName,
            employee.WorkEmail,
            employee.PhoneNumber,
            employee.HireDate,
            employee.EmploymentStatus,
            employee.EmploymentType,
            employee.WorkLocation,
            employee.DepartmentName,
            employee.PositionTitle,
            employee.ManagerName);
}
