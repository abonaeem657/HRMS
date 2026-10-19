using System.Net;
using System.Text.RegularExpressions;
using HRMS.Application.Identity;
using HRMS.Domain.Employees;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace HRMS.IntegrationTests;

public partial class SqlWorkflowTests
{
    [SqlFact]
    public async Task RealIdentityLoginWorksForAllRolesAndEmployeeSubmitsDateOnlyLeave() => await WithDatabase(async db =>
    {
        var (departmentId, positionId) = await Organization(db);
        var connection = db.Database.GetConnectionString()!;
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(log => log.ClearProviders());
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:HRMS"] = connection,
                ["Identity:AllowInitialAdministratorSetup"] = "false"
            }));
            builder.ConfigureServices(services =>
            {
                // Explicitly isolate every real Identity/HTTP operation from HRMSnew.
                services.RemoveAll<DbContextOptions<HRMSDbContext>>();
                services.AddDbContext<HRMSDbContext>(options => options.UseSqlServer(connection));
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
        });
        var password = "Aa1!" + Guid.NewGuid().ToString("N");
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<HRMSUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in IdentityRoles.All)
            {
                Assert.True((await roles.CreateAsync(new(role.Name))).Succeeded);
                var email = role.Name.ToLowerInvariant() + "@example.test";
                var user = new HRMSUser { UserName = email, Email = email, IsActive = true };
                Assert.True((await users.CreateAsync(user, password)).Succeeded);
                Assert.True((await users.AddToRoleAsync(user, role.Name)).Succeeded);
                var employee = new Employee(role.Name, "Test", role.Name, email, new(2026, 1, 1), "Active", departmentId, positionId, "FullTime", identityUserId: user.Id);
                db.Employees.Add(employee);
                await db.SaveChangesAsync();
                if (role.Name == IdentityRoles.DepartmentManager)
                {
                    (await db.Departments.SingleAsync()).AssignManager(employee.EmployeeId);
                    await db.SaveChangesAsync();
                }
            }
        }
        var leaveType = new HRMS.Domain.LeaveManagement.LeaveType("Annual", "سنوية", 14);
        db.LeaveTypes.Add(leaveType);
        await db.SaveChangesAsync();
        foreach (var role in IdentityRoles.All)
        {
            using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
            client.DefaultRequestHeaders.Add("Accept-Language", "ar-JO");
            var loginPage = await client.GetStringAsync("/Account/Login");
            var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(loginPage),
                ["Email"] = role.Name.ToLowerInvariant() + "@example.test",
                ["Password"] = password,
                ["RememberMe"] = "false"
            }));
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            Assert.Equal("/", login.Headers.Location!.OriginalString);
            using var homeResponse = await client.GetAsync("/");
            var homeBody = await homeResponse.Content.ReadAsStringAsync();
            Assert.True(homeResponse.IsSuccessStatusCode, homeBody);
            Assert.Contains("workspace-layout", homeBody);
            Assert.Contains("guide-step", await client.GetStringAsync("/Home/Guide"));
            var profile = await client.GetStringAsync("/MyProfile?employeeId=999999");
            var profileBody = profile[profile.IndexOf("<main", StringComparison.Ordinal)..profile.IndexOf("</main>", StringComparison.Ordinal)];
            Assert.Contains(role.Name.ToLowerInvariant() + "@example.test", profileBody);
            Assert.DoesNotContain("ProfileReadOnlyHint", profileBody);
            foreach (var (path, allowed) in new[]
            {
                ("/UserAccounts", role.Name == IdentityRoles.SystemAdministrator),
                ("/AuditLogs", role.Name is IdentityRoles.SystemAdministrator or IdentityRoles.HRManager or IdentityRoles.Auditor),
                ("/Reports", role.Name != IdentityRoles.Employee),
                ("/LeaveManagement/MyReport", role.Name != IdentityRoles.Auditor),
                ("/Attendance/Correct", role.Name is IdentityRoles.SystemAdministrator or IdentityRoles.HRManager or IdentityRoles.HROfficer)
            })
            {
                var response = await client.GetAsync(path);
                if (allowed) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                else
                {
                    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                    Assert.Contains("/Account/AccessDenied", response.Headers.Location!.OriginalString);
                }
            }
            if (role.Name != IdentityRoles.Employee) continue;
            foreach (var (culture, day) in new[] { ("ar-JO", "05"), ("en-US", "06") })
            {
                client.DefaultRequestHeaders.Remove("Accept-Language");
                client.DefaultRequestHeaders.Add("Accept-Language", culture);
                var page = await client.GetStringAsync("/LeaveManagement/Create");
                Assert.DoesNotContain("name=\"StartTime\"", page);
                Assert.DoesNotContain("name=\"EndTime\"", page);
                var submit = await client.PostAsync("/LeaveManagement/Create", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = Token(page), ["LeaveTypeId"] = leaveType.LeaveTypeId.ToString(),
                    ["StartDate"] = "2027-06-" + day, ["EndDate"] = "2027-06-" + day,
                    ["StartTime"] = "13:30", ["EndTime"] = "", ["Reason"] = "Date-only leave"
                }));
                Assert.Equal(HttpStatusCode.Redirect, submit.StatusCode);
                Assert.Equal("/LeaveManagement/MyReport", submit.Headers.Location!.OriginalString);
                var report = await client.GetStringAsync("/LeaveManagement/MyReport?year=2027&employeeId=1");
                Assert.Contains("Date-only leave", report);
                Assert.DoesNotContain("SystemAdministrator@example.test", report, StringComparison.OrdinalIgnoreCase);
                using var ownExport = await client.GetAsync("/LeaveManagement/MyReport?year=2027&employeeId=1&export=xlsx");
                Assert.Equal(HttpStatusCode.OK, ownExport.StatusCode);
                var workbook = WorkbookText(await ownExport.Content.ReadAsByteArrayAsync());
                Assert.Contains("Date-only leave", workbook);
                Assert.DoesNotContain("SystemAdministrator@example.test", workbook, StringComparison.OrdinalIgnoreCase);
                Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Reports/Employees?export=xlsx")).StatusCode);
            }
        }
        db.ChangeTracker.Clear();
        Assert.Equal(6, await db.AuditLogs.CountAsync(a => a.Action == "Authentication.LoginSucceeded"));
        var requests = await db.LeaveRequests.ToListAsync();
        Assert.Equal(2, requests.Count);
        Assert.All(requests, r => { Assert.Null(r.StartTime); Assert.Null(r.EndTime); });
        Assert.Empty(await db.AttendanceRecords.ToListAsync());
    });

    private static string Token(string html)
    {
        var input = Regex.Matches(html, "<input\\b[^>]*>").Select(m => m.Value).First(s => s.Contains("name=\"__RequestVerificationToken\""));
        return WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]+)\"").Groups[1].Value);
    }
}
