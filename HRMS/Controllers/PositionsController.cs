
using HRMS.Application.Organization;
using HRMS.Application.Identity;
using HRMS.Models.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRMS.Controllers;

[Authorize]
public sealed class PositionsController(IPositionService positionService) : Controller
{
    [Authorize(Roles = IdentityRoles.ReadHrData)]
    public async Task<IActionResult> Index(
        [FromQuery] PositionListViewModel query,
        CancellationToken cancellationToken)
    {
        var positions = await positionService.GetListAsync(
            new PositionListQuery(query.Search, query.DepartmentId, query.IsActive),
            cancellationToken);

        var departments = await positionService.GetDepartmentsAsync(
            includeInactive: true, cancellationToken: cancellationToken);

        query.Positions = positions.Select(position => new PositionListItemViewModel(
            position.PositionId,
            position.Code,
            position.Title,
            position.DepartmentName,
            position.EmployeeCount,
            position.IsActive,
            position.Grade
        )).ToList();

        query.Departments = departments.Select(department => new SelectListItem(
            department.Text,
            department.Id.ToString(),
            department.Id == query.DepartmentId
        )).ToList();

        return IsAjaxRequest()
            ? PartialView("_Results", query)
            : View(query);
    }

    [Authorize(Roles = IdentityRoles.ReadHrData)]
    public async Task<IActionResult> Details(
        int id,
        CancellationToken cancellationToken)
    {
        var position = await positionService.GetByIdAsync(id, cancellationToken);

        if (position is null)
        {
            return NotFound();
        }

        var model = new PositionDetailsViewModel(
            position.PositionId,
            position.Code,
            position.Title,
            position.DepartmentName,
            position.Description,
            position.Grade,
            position.IsActive,
            position.EmployeeCount
        );

        return IsModalRequest()
            ? PartialView("_DetailsModal", model)
            : View(model);
    }

    [Authorize(Roles = IdentityRoles.ManageOrganization)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new PositionFormViewModel();
        await PopulateDepartmentsAsync(model, cancellationToken);

        return IsModalRequest()
            ? PartialView("_FormModal", model)
            : View(model);
    }

    [HttpPost]
    [Authorize(Roles = IdentityRoles.ManageOrganization)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        PositionFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDepartmentsAsync(model, cancellationToken);
            return IsAjaxRequest()
                ? PartialView("_FormModal", model)
                : View(model);
        }

        var result = await positionService.CreateAsync(
            new PositionInput(
                model.Code,
                model.Title,
                model.DepartmentId,
                model.Description,
                model.Grade),
            cancellationToken);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            await PopulateDepartmentsAsync(model, cancellationToken);

            return IsAjaxRequest()
                ? PartialView("_FormModal", model)
                : View(model);
        }

        if (IsAjaxRequest())
        {
            return Json(new { message = HRMS.Application.Common.Messages.Get("Business038") });
        }

        TempData["StatusMessage"] = HRMS.Application.Common.Messages.Get("Business038");
        return RedirectToAction(nameof(Details), new { id = result.Id });
    }

    [Authorize(Roles = IdentityRoles.ManageOrganization)]
    public async Task<IActionResult> Edit(
        int id,
        CancellationToken cancellationToken)
    {
        var position = await positionService.GetByIdAsync(id, cancellationToken);

        if (position is null)
        {
            return NotFound();
        }

        var model = new PositionFormViewModel
        {
            Code = position.Code,
            Title = position.Title,
            DepartmentId = position.DepartmentId,
            Description = position.Description,
            Grade = position.Grade
        };

        await PopulateDepartmentsAsync(model, cancellationToken);
        ViewData["PositionId"] = id;

        return IsModalRequest()
            ? PartialView("_FormModal", model)
            : View(model);
    }

    [HttpPost]
    [Authorize(Roles = IdentityRoles.ManageOrganization)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        PositionFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDepartmentsAsync(model, cancellationToken);
            ViewData["PositionId"] = id;

            return IsAjaxRequest()
                ? PartialView("_FormModal", model)
                : View(model);
        }

        var result = await positionService.UpdateAsync(
            id,
            new PositionInput(
                model.Code,
                model.Title,
                model.DepartmentId,
                model.Description,
                model.Grade),
            cancellationToken);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            await PopulateDepartmentsAsync(model, cancellationToken);
            ViewData["PositionId"] = id;

            return IsAjaxRequest()
                ? PartialView("_FormModal", model)
                : View(model);
        }

        if (IsAjaxRequest())
        {
            return Json(new { message = HRMS.Application.Common.Messages.Get("Business029") });
        }

        TempData["StatusMessage"] = HRMS.Application.Common.Messages.Get("Business029");
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
                ? BadRequest(new { message = HRMS.Application.Common.Messages.Get("Business045") })
                : BadRequest(ModelState);
        }

        if (!await positionService.SetActiveAsync(id, isActive, cancellationToken))
        {
            return IsAjaxRequest()
                ? NotFound(new { message = HRMS.Application.Common.Messages.Get("Business026") })
                : NotFound();
        }

        var message = isActive
            ? HRMS.Application.Common.Messages.Get("Business036")
            : HRMS.Application.Common.Messages.Get("Business033");

        if (IsAjaxRequest())
        {
            return Json(new { message, isActive });
        }

        TempData["StatusMessage"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateDepartmentsAsync(
        PositionFormViewModel model,
        CancellationToken cancellationToken)
    {
        var departments = await positionService.GetDepartmentsAsync(cancellationToken: cancellationToken);

        model.Departments = departments.Select(department => new SelectListItem(
            department.Text,
            department.Id.ToString(),
            department.Id == model.DepartmentId
        )).ToList();
    }

    private void AddErrors(IReadOnlyDictionary<string, string[]> errors)
    {
        foreach (var (key, messages) in errors)
        {
            var modelKey = key switch
            {
                nameof(PositionInput.Code) => nameof(PositionFormViewModel.Code),
                nameof(PositionInput.Title) => nameof(PositionFormViewModel.Title),
                nameof(PositionInput.DepartmentId) => nameof(PositionFormViewModel.DepartmentId),
                nameof(PositionInput.Description) => nameof(PositionFormViewModel.Description),
                nameof(PositionInput.Grade) => nameof(PositionFormViewModel.Grade),
                _ => string.Empty
            };

            foreach (var message in messages)
            {
                ModelState.AddModelError(modelKey, message);
            }
        }
    }

    private bool IsAjaxRequest() =>
        string.Equals(
            Request.Headers["X-Requested-With"].ToString(),
            "XMLHttpRequest",
            StringComparison.Ordinal);

    private bool IsModalRequest() =>
        bool.TryParse(Request.Query["modal"].ToString(), out var isModal) && isModal;
}
