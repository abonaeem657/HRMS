using System.IO.Compression;
using System.Net;
using System.Xml.Linq;
using HRMS.Application.Identity;
using HRMS.Application.LeaveManagement;
using HRMS.Domain.Employees;
using HRMS.Domain.LeaveManagement;
using HRMS.Domain.Organization;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using HRMS.Infrastructure.Auditing;
using Moq;

namespace HRMS.IntegrationTests;

public partial class SqlWorkflowTests
{
    private static string WorkbookText(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes));
        return string.Join("\n", zip.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/")).Select(e => { using var s = e.Open(); return XDocument.Load(s).Root!.Value; }));
    }

    [SqlFact]
    public async Task ReportsDownloadRealPdfAndExcelWithAllFilteredRowsAndManagerScope() => await WithDatabase(async db =>
    {
        var (department, position) = await Organization(db);
        for (var i = 1; i <= 7; i++) db.Employees.Add(new Employee($"EXPORT-{i}", "أحمد", $"Employee{i}", $"export{i}@example.test", new(2026, 1, 1), "Active", department, position, "FullTime"));
        var other = new Department("OTHER", "Other department", true); db.Departments.Add(other); await db.SaveChangesAsync();
        var otherPosition = new Position("OTHER", "Other position", true, other.DepartmentId); db.Positions.Add(otherPosition); await db.SaveChangesAsync();
        db.Employees.Add(new Employee("PRIVATE-OTHER", "Outside", "Department", "outside@example.test", new(2026, 1, 1), "Active", other.DepartmentId, otherPosition.PositionId, "FullTime"));
        await db.SaveChangesAsync();
        await using var app = DisplayAndLoginApp(db);
        var password = "Aa1!" + Guid.NewGuid().ToString("N");
        await CreateDisplayAdmin(app, password);
        using var client = app.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Accept-Language", "ar-JO");
        Assert.Equal(HttpStatusCode.Redirect, (await DisplayLogin(client, password)).StatusCode);
        using var excel = await client.GetAsync($"/Reports/Employees?departmentId={department}&Page=2&export=xlsx");
        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
        Assert.Contains("spreadsheetml", excel.Content.Headers.ContentType!.MediaType);
        var text = WorkbookText(await excel.Content.ReadAsByteArrayAsync());
        Assert.Contains("EXPORT-1", text); Assert.Contains("EXPORT-7", text); Assert.Contains("أحمد", text); Assert.DoesNotContain("PRIVATE-OTHER", text);
        using var pdf = await client.GetAsync($"/Reports/Employees?departmentId={department}&export=pdf");
        Assert.True(pdf.IsSuccessStatusCode, await pdf.Content.ReadAsStringAsync());
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync())[..5]));
        Assert.Contains("no-store", pdf.Headers.CacheControl!.ToString());
        foreach (var route in new[] { "Index", "Attendance", "Leaves", "LeaveDecisions", "Headcount" })
        {
            using var download = await client.GetAsync("/Reports/" + route + "?export=xlsx");
            Assert.True(download.IsSuccessStatusCode, route + ": " + await download.Content.ReadAsStringAsync());
            Assert.NotEmpty(WorkbookText(await download.Content.ReadAsByteArrayAsync()));
        }
        using (var audit = await client.GetAsync("/AuditLogs?export=xlsx")) Assert.Equal(HttpStatusCode.OK, audit.StatusCode);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<HRMSUser>>();
            Assert.True((await roles.CreateAsync(new(IdentityRoles.DepartmentManager))).Succeeded);
            var manager = new HRMSUser { Email = "manager-export@example.test", UserName = "manager-export@example.test", IsActive = true };
            Assert.True((await users.CreateAsync(manager, password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(manager, IdentityRoles.DepartmentManager)).Succeeded);
            var employee = await db.Employees.FirstAsync(e => e.DepartmentId == department);
            employee.LinkIdentityUser(manager.Id);
            (await db.Departments.SingleAsync(d => d.DepartmentId == department)).AssignManager(employee.EmployeeId);
            await db.SaveChangesAsync();
        }
        using var managerClient = app.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        var login = await managerClient.GetStringAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.Redirect, (await managerClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(login), ["Email"] = "manager-export@example.test", ["Password"] = password }))).StatusCode);
        text = WorkbookText(await managerClient.GetByteArrayAsync($"/Reports/Employees?departmentId={other.DepartmentId}&export=xlsx"));
        Assert.Contains("EXPORT-7", text); Assert.DoesNotContain("PRIVATE-OTHER", text);
    });

    [SqlFact]
    public async Task LeavePolicyKeepsThreeTypesRejectsUnsupportedRequestsAndPreservesHistory() => await WithDatabase(async db =>
    {
        var (department, position) = await Organization(db);
        var employee = new Employee("LEGACY", "Legacy", "Employee", "legacy@example.test", new(2026, 1, 1), "Active", department, position, "FullTime");
        var legacy = new LeaveType("Emergency", "طارئة", 3);
        db.AddRange(employee, legacy); await db.SaveChangesAsync();
        db.LeaveRequests.Add(new LeaveRequest(employee.EmployeeId, legacy.LeaveTypeId, new(2027, 1, 1), new(2027, 1, 1), "Keep history"));
        await db.SaveChangesAsync();
        await using var app = DisplayAndLoginApp(db);
        await using var scope = app.Services.CreateAsyncScope();
        await ReferenceDataSeed.RunAsync(scope.ServiceProvider);
        db.ChangeTracker.Clear();
        Assert.False((await db.LeaveTypes.SingleAsync(t => t.LeaveTypeId == legacy.LeaveTypeId)).IsActive);
        Assert.Equal("Keep history", (await db.LeaveRequests.SingleAsync()).Reason);
        var service = scope.ServiceProvider.GetRequiredService<ILeaveService>();
        Assert.Equal(new[] { "Annual", "Sick", "Unpaid" }, (await service.GetLeaveTypesAsync()).Select(t => t.Name));
        var reporting = scope.ServiceProvider.GetRequiredService<HRMS.Application.Reporting.IReportingService>();
        Assert.Equal(3, (await reporting.GetFilterOptionsAsync()).LeaveTypes.Count);
        Assert.False((await service.CreateLeaveTypeAsync(new("Study", "دراسة", 5, true, true))).Succeeded);
        // Even a legacy type reactivated outside the UI cannot be used for a new request.
        var stored = await db.LeaveTypes.SingleAsync(t => t.LeaveTypeId == legacy.LeaveTypeId);
        stored.Update(stored.Name, stored.NameAr, stored.DefaultDaysPerYear, stored.IsPaid, true);
        await db.SaveChangesAsync();
        var access = new Mock<IRecordAccess>();
        access.Setup(a => a.CanAccessEmployeeAsync(employee.EmployeeId, false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var writer = new LeaveService(new ModuleData(db), new AuditLogService(db, new HttpContextAccessor()), NullLogger<LeaveService>.Instance, access.Object);
        Assert.False((await writer.SubmitLeaveRequestAsync(new(employee.EmployeeId, legacy.LeaveTypeId, new(2027, 2, 1), new(2027, 2, 1), "Unsupported type"))).Succeeded);
        Assert.Equal(1, await db.LeaveRequests.CountAsync());
    });
}
