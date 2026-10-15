using System.Security.Claims;
using System.Text.Json;
using HRMS.Application.Employees;
using HRMS.Application.External;
using HRMS.Application.Identity;
using HRMS.Application.LeaveManagement;
using HRMS.Application.Organization;
using HRMS.Models.Api;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.RateLimiting;

namespace HRMS.Controllers.Api.V1;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
[EnableRateLimiting("api")]
public sealed class HrmsApiController(
    IEmployeeService employeeService,
    IDepartmentService departmentService,
    ILeaveService leaveService,
    IPublicHolidaysService publicHolidaysService,
    IUserAccountService userAccountService,
    IAntiforgery antiforgery,
    ILogger<HrmsApiController> logger) : ControllerBase
{
    [HttpGet("antiforgery")]
    [Authorize]
    public IActionResult GetAntiforgeryToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { requestToken = tokens.RequestToken });
    }

    [HttpGet("employees")]
    [Authorize(Roles = IdentityRoles.ReadHrData)]
    public async Task<IActionResult> GetEmployees(
        [FromQuery] string? search,
        [FromQuery] int? departmentId,
        [FromQuery] string? status,
        [FromQuery] string? sortBy,
        [FromQuery] bool sortDescending,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await employeeService.SearchAsync(
            new EmployeeListQuery(search, departmentId, status, sortBy, sortDescending, page, pageSize),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("employees/{id:int}")]
    [Authorize(Roles = IdentityRoles.ReadHrData)]
    public async Task<IActionResult> GetEmployee(int id, CancellationToken cancellationToken)
    {
        var employee = await employeeService.GetByIdAsync(id, cancellationToken);
        return employee is null ? NotFound(ProblemDetailsFor(404, HRMS.Application.Common.Messages.Get("Business016"))) : Ok(employee);
    }

    [HttpPost("employees")]
    [Authorize(Roles = IdentityRoles.ManageEmployees)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEmployee(
        EmployeeApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await employeeService.CreateAsync(ToInput(request), cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new ValidationProblemDetails(
                result.Errors.ToDictionary(pair => pair.Key, pair => pair.Value)));
        }
        var employee = await employeeService.GetByIdAsync(result.EmployeeId!.Value, cancellationToken);
        return CreatedAtAction(nameof(GetEmployee), new { id = result.EmployeeId }, employee);
    }

    [HttpPut("employees/{id:int}")]
    [Authorize(Roles = IdentityRoles.ManageEmployees)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEmployee(
        int id,
        EmployeeApiRequest request,
        CancellationToken cancellationToken)
    {
        if (await employeeService.GetByIdAsync(id, cancellationToken) is null)
            return NotFound(ProblemDetailsFor(404, HRMS.Application.Common.Messages.Get("Business016")));
        var result = await employeeService.UpdateAsync(id, ToInput(request), cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new ValidationProblemDetails(
                result.Errors.ToDictionary(pair => pair.Key, pair => pair.Value)));
        }
        return Ok(await employeeService.GetByIdAsync(id, cancellationToken));
    }

    [HttpDelete("employees/{id:int}")]
    [Authorize(Roles = IdentityRoles.ManageEmployees)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEmployee(int id, CancellationToken cancellationToken)
    {
        var deleted = await employeeService.SoftDeleteAsync(
            id,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            cancellationToken);
        return deleted ? NoContent() : NotFound(ProblemDetailsFor(404, HRMS.Application.Common.Messages.Get("Business016")));
    }

    [HttpGet("departments")]
    [Authorize(Roles = IdentityRoles.ReadHrData)]
    public async Task<IActionResult> GetDepartments([FromQuery] string? search, [FromQuery] bool? isActive,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(await departmentService.GetListAsync(new DepartmentListQuery(search, isActive, page, pageSize), cancellationToken));

    [HttpGet("leave-requests")]
    [Authorize(Roles = IdentityRoles.UseLeaves)]
    public async Task<IActionResult> GetLeaveRequests(
        [FromQuery] string? status,
        [FromQuery] int? leaveTypeId,
        [FromQuery] int? year,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (year is < 2000 or > 2100)
        {
            return BadRequest(ProblemDetailsFor(400, HRMS.Application.Common.Messages.Get("Business063")));
        }
        var isHr = User.IsInRole(IdentityRoles.SystemAdministrator) ||
                   User.IsInRole(IdentityRoles.HRManager) ||
                   User.IsInRole(IdentityRoles.HROfficer);
        int? employeeId = null;
        int? departmentId = null;
        if (!isHr)
        {
            employeeId = await GetLinkedEmployeeIdAsync(cancellationToken);
            if (employeeId is null)
            {
                return Forbid();
            }
            if (User.IsInRole(IdentityRoles.DepartmentManager))
            {
                var employee = await employeeService.GetByIdAsync(employeeId.Value, cancellationToken);
                departmentId = employee?.DepartmentId;
                var department = departmentId is int id
                    ? await departmentService.GetByIdAsync(id, cancellationToken)
                    : null;
                if (department?.ManagerEmployeeId != employeeId.Value)
                {
                    return Forbid();
                }
                employeeId = null;
            }
        }

        var results = await leaveService.GetLeaveRequestsAsync(
            new LeaveFilter(employeeId, departmentId, leaveTypeId, status, year, page, pageSize),
            cancellationToken);
        return Ok(results);
    }

    [HttpGet("leave-requests/{id:int}")]
    [Authorize(Roles = IdentityRoles.UseLeaves)]
    public async Task<IActionResult> GetLeaveRequest(int id, CancellationToken cancellationToken)
    {
        var request = await leaveService.GetLeaveRequestByIdAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(ProblemDetailsFor(404, HRMS.Application.Common.Messages.Get("Business043")));
        }

        var isHr = User.IsInRole(IdentityRoles.SystemAdministrator) ||
                   User.IsInRole(IdentityRoles.HRManager) ||
                   User.IsInRole(IdentityRoles.HROfficer);
        if (!isHr)
        {
            var employeeId = await GetLinkedEmployeeIdAsync(cancellationToken);
            if (employeeId is null)
            {
                return Forbid();
            }
            if (User.IsInRole(IdentityRoles.DepartmentManager))
            {
                var employee = await employeeService.GetByIdAsync(employeeId.Value, cancellationToken);
                var department = employee is null
                    ? null
                    : await departmentService.GetByIdAsync(employee.DepartmentId, cancellationToken);
                if (employee?.DepartmentId != request.DepartmentId ||
                    department?.ManagerEmployeeId != employeeId.Value)
                {
                    return Forbid();
                }
            }
            else if (request.EmployeeId != employeeId.Value)
            {
                return Forbid();
            }
        }
        return Ok(request);
    }

    [HttpGet("public-holidays/{year:int}")]
    [Authorize(Roles = IdentityRoles.ViewReports)]
    public async Task<IActionResult> GetPublicHolidays(
        int year,
        [FromQuery] string countryCode = "JO",
        CancellationToken cancellationToken = default)
    {
        if (year is < 2000 or > 2100 || countryCode.Length != 2 ||
            !countryCode.All(char.IsAsciiLetter))
        {
            return BadRequest(ProblemDetailsFor(400, HRMS.Application.Common.Messages.Get("Business005")));
        }
        try
        {
            return Ok(await publicHolidaysService.GetHolidaysAsync(year, countryCode, cancellationToken));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Public holidays provider timed out for {CountryCode} in {Year}.", countryCode, year);
            return StatusCode(504, ProblemDetailsFor(504, HRMS.Application.Common.Messages.Get("Extra007")));
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Public holidays provider request failed for {CountryCode} in {Year}.", countryCode, year);
            return StatusCode(502, ProblemDetailsFor(502, HRMS.Application.Common.Messages.Get("Business023")));
        }
        catch (InvalidDataException exception)
        {
            logger.LogWarning(exception, "Public holidays provider returned invalid data for {CountryCode} in {Year}.", countryCode, year);
            return StatusCode(502, ProblemDetailsFor(502, HRMS.Application.Common.Messages.Get("Business001")));
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Public holidays provider returned invalid JSON for {CountryCode} in {Year}.", countryCode, year);
            return StatusCode(502, ProblemDetailsFor(502, HRMS.Application.Common.Messages.Get("Business000")));
        }
    }

    [HttpPost("leave-requests")]
    [Authorize(Roles = IdentityRoles.UseLeaves)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLeaveRequest(
        [FromBody] CreateLeaveRequestRequest request,
        CancellationToken cancellationToken)
    {
        var employeeId = await GetLinkedEmployeeIdAsync(cancellationToken);
        if (employeeId is null)
        {
            return Forbid();
        }

        var result = await leaveService.SubmitLeaveRequestAsync(
            new CreateLeaveRequestCommand(
                employeeId.Value,
                request.LeaveTypeId,
                request.StartDate,
                request.EndDate,
                request.Reason),
            cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(ProblemDetailsFor(400, string.Join(" ", result.Errors)));
        }
        return Created($"/api/v1/leave-requests/{result.LeaveRequestId}", new { id = result.LeaveRequestId });
    }

    private async Task<int?> GetLinkedEmployeeIdAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null
            ? null
            : await userAccountService.GetLinkedEmployeeIdAsync(userId, cancellationToken);
    }

    private static EmployeeInput ToInput(EmployeeApiRequest request) =>
        new(
            request.EmployeeNumber,
            request.FirstName,
            request.LastName,
            request.WorkEmail,
            request.HireDate!.Value,
            request.EmploymentStatus,
            request.EmploymentType,
            request.DepartmentId,
            request.PositionId,
            request.ManagerEmployeeId,
            request.PhoneNumber,
            request.WorkLocation, request.RowVersion);

    private static ProblemDetails ProblemDetailsFor(int status, string detail) =>
        new()
        {
            Status = status,
            Title = status == 404 ? "Resource not found" : "Request failed",
            Detail = detail
        };
}

public sealed class CreateLeaveRequestRequest
{
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
    public int LeaveTypeId { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    public DateOnly StartDate { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    public DateOnly EndDate { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(1000)]
    public string Reason { get; set; } = string.Empty;
}
