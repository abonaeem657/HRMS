using HRMS.Models;
using HRMS.Application.Organization;
using HRMS.Application.Identity;
using HRMS.Models.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRMS.Controllers;

[Authorize]
public sealed class DepartmentsController(IDepartmentService departmentService) : Controller
{
    [Authorize(Roles = IdentityRoles.ReadHrData)]
    public async Task<IActionResult> Index(
        [FromQuery] DepartmentListViewModel query,
        CancellationToken cancellationToken)
    {
        var result = await departmentService.SearchAsync(
            new DepartmentListQuery(query.Search, query.IsActive),
            DisplayPagination.Request(Request),
            cancellationToken);

        ViewData["Pagination"] = DisplayPagination.Create(Request, result.TotalCount, result.Page);
        query.Departments = result.Items
            .Select(department => new DepartmentListItemViewModel(
                department.DepartmentId,
                department.Code,
                department.Name,
                department.ManagerName,
                department.EmployeeCount,
                department.IsActive))
            .ToList();

        return IsAjaxRequest()
            ? PartialView("_Results", query)
            : View(query);
    }

    [Authorize(Roles = IdentityRoles.ReadHrData)]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var department = await departmentService.GetByIdAsync(id, cancellationToken, includeEmployees: false);
        if (department is null)
        {
            return NotFound();
        }

        var employees = await departmentService.GetEmployeesAsync(id, DisplayPagination.Request(Request), cancellationToken);
        ViewData["Pagination"] = DisplayPagination.Create(Request, employees.TotalCount, employees.Page);
        var model = ToDetailsViewModel(department with { Employees = employees.Items });
        return IsModalRequest()
            ? PartialView("_DetailsModal", model)
            : View(model);
    }

    [Authorize(Roles = IdentityRoles.ManageOrganization)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new DepartmentFormViewModel();
        await PopulateManagersAsync(model, null, cancellationToken);
        return IsModalRequest()
            ? PartialView("_FormModal", model)
            : View(model);
    }

    [HttpPost]
    [Authorize(Roles = IdentityRoles.ManageOrganization)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        DepartmentFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateManagersAsync(model, null, cancellationToken);
            return IsAjaxRequest()
                ? PartialView("_FormModal", model)
                : View(model);
        }

        var result = await departmentService.CreateAsync(
            new DepartmentInput(model.Code, model.Name, model.ManagerEmployeeId),
            cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            await PopulateManagersAsync(model, null, cancellationToken);
            return IsAjaxRequest()
                ? PartialView("_FormModal", model)
                : View(model);
        }

        if (IsAjaxRequest())
        {
            return Json(new { message = HRMS.Application.Common.Messages.Get("Business037") });
        }

        TempData["StatusMessage"] = HRMS.Application.Common.Messages.Get("Business037");
        return RedirectToAction(nameof(Details), new { id = result.Id });
    }

    [Authorize(Roles = IdentityRoles.ManageOrganization)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var department = await departmentService.GetByIdAsync(id, cancellationToken);
        if (department is null)
        {
            return NotFound();
        }

        var model = new DepartmentFormViewModel
        {
            Code = department.Code,
            Name = department.Name,
            ManagerEmployeeId = department.ManagerEmployeeId
        };
        await PopulateManagersAsync(model, null, cancellationToken);
        ViewData["DepartmentId"] = id;
        return IsModalRequest()
            ? PartialView("_FormModal", model)
            : View(model);
    }

    [HttpPost]
    [Authorize(Roles = IdentityRoles.ManageOrganization)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        DepartmentFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateManagersAsync(model, null, cancellationToken);
            ViewData["DepartmentId"] = id;
            return IsAjaxRequest()
                ? PartialView("_FormModal", model)
                : View(model);
        }

        var result = await departmentService.UpdateAsync(
            id,
            new DepartmentInput(model.Code, model.Name, model.ManagerEmployeeId),
            cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            await PopulateManagersAsync(model, null, cancellationToken);
            ViewData["DepartmentId"] = id;
            return IsAjaxRequest()
                ? PartialView("_FormModal", model)
                : View(model);
        }

        if (IsAjaxRequest())
        {
            return Json(new { message = HRMS.Application.Common.Messages.Get("Business028") });
        }

        TempData["StatusMessage"] = HRMS.Application.Common.Messages.Get("Business028");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Roles = IdentityRoles.ManageOrganization)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(
        int id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || id <= 0)
        {
            return IsAjaxRequest()
                ? BadRequest(new { message = HRMS.Application.Common.Messages.Get("Business044") })
                : BadRequest(ModelState);
        }

        if (!await departmentService.SetActiveAsync(id, isActive, cancellationToken))
        {
            if (IsAjaxRequest())
            {
                return NotFound(new { message = HRMS.Application.Common.Messages.Get("Business025") });
            }

            return NotFound();
        }

        var message = isActive
            ? HRMS.Application.Common.Messages.Get("Business035")
            : HRMS.Application.Common.Messages.Get("Business032");
        if (IsAjaxRequest())
        {
            return Json(new { message, isActive });
        }

        TempData["StatusMessage"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateManagersAsync(
        DepartmentFormViewModel model,
        int? excludeManagerEmployeeId,
        CancellationToken cancellationToken)
    {
        var options = await departmentService.GetFormOptionsAsync(
            excludeManagerEmployeeId,
            cancellationToken);
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
            var modelKey = key == nameof(DepartmentInput.ManagerEmployeeId)
                ? nameof(DepartmentFormViewModel.ManagerEmployeeId)
                : string.Empty;
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

    private static DepartmentDetailsViewModel ToDetailsViewModel(DepartmentDetails department) =>
        new(
            department.DepartmentId,
            department.Code,
            department.Name,
            department.ManagerName,
            department.IsActive,
            department.Employees
                .Select(employee => new DepartmentEmployeeViewModel(
                    employee.EmployeeId,
                    employee.EmployeeNumber,
                    employee.FullName,
                    employee.PositionTitle,
                    employee.EmploymentStatus))
                .ToList());
}
