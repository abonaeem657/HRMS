using System.Security.Claims;
using HRMS.Application.Employees;
using HRMS.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.Controllers;

[Authorize]
public sealed class MyProfileController(IEmployeeService employees) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Challenge();
        // No employee ID is accepted from the request: this page always belongs to the session owner.
        var employee = await employees.GetOwnProfileAsync(userId, cancellationToken);
        return employee is null ? View("Unlinked") : View(employee);
    }
}
