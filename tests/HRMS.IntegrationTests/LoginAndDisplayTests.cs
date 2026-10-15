using System.Net;
using System.Text.RegularExpressions;
using HRMS.Application.Employees;
using HRMS.Application.Identity;
using HRMS.Domain.Employees;
using HRMS.Domain.Organization;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
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
    private static WebApplicationFactory<Program> DisplayAndLoginApp(HRMSDbContext db) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            var connection = db.Database.GetConnectionString()!;
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(log => log.ClearProviders());
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:HRMS"] = connection }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<HRMSDbContext>>();
                services.AddDbContext<HRMSDbContext>(options => options.UseSqlServer(connection));
            });
        });

    private static async Task CreateDisplayAdmin(WebApplicationFactory<Program> app, string password)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<HRMSUser>>();
        Assert.True((await roles.CreateAsync(new(IdentityRoles.SystemAdministrator))).Succeeded);
        var user = new HRMSUser { UserName = "display@example.test", Email = "display@example.test", IsActive = true };
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, IdentityRoles.SystemAdministrator)).Succeeded);
    }

    private static async Task<HttpResponseMessage> DisplayLogin(HttpClient client, string password)
    {
        var html = await client.GetStringAsync("/Account/Login");
        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html), ["Email"] = "display@example.test", ["Password"] = password
        }));
    }

    [SqlFact]
    public async Task LoginWarnsAfterSecondFailureLocksForOneHourAndAllowsRetryAfterExpiry() => await WithDatabase(async db =>
    {
        await using var app = DisplayAndLoginApp(db);
        var password = "Aa1!" + Guid.NewGuid().ToString("N");
        await CreateDisplayAdmin(app, password);
        using var client = app.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Accept-Language", "en-US");
        for (var failure = 1; failure <= 5; failure++)
        {
            using var response = await DisplayLogin(client, "Incorrect!12345");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            if (failure == 1) Assert.DoesNotContain("login-attempt-warning", html);
            else if (failure < 5) Assert.Contains($"You have {5 - failure} attempts remaining", html);
            else Assert.Contains("Try again in 60 minutes", html);
        }
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<HRMSUser>>();
            var user = (await users.FindByEmailAsync("display@example.test"))!;
            Assert.True(await users.IsLockedOutAsync(user));
            Assert.InRange((user.LockoutEnd!.Value - DateTimeOffset.UtcNow).TotalMinutes, 59, 60);
        }
        using (var blocked = await DisplayLogin(client, password))
        {
            Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);
            Assert.Contains("temporarily locked", await blocked.Content.ReadAsStringAsync());
        }
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<HRMSUser>>();
            var user = (await users.FindByEmailAsync("display@example.test"))!;
            Assert.True((await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddSeconds(-1))).Succeeded);
        }
        using var success = await DisplayLogin(client, password);
        Assert.Equal(HttpStatusCode.Redirect, success.StatusCode);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<HRMSUser>>();
            Assert.Equal(0, await users.GetAccessFailedCountAsync((await users.FindByEmailAsync("display@example.test"))!));
        }
    });

    [SqlFact]
    public async Task WebListsShowSixRowsAndShowAllKeepsFiltersBeyondOneHundredRows() => await WithDatabase(async db =>
    {
        var (department, position) = await Organization(db);
        for (var i = 1; i <= 107; i++) db.Employees.Add(new Employee($"PAG-{i:000}", "Pagination", $"Employee{i}", $"pagination{i}@example.test", new(2026, 1, 1), "Active", department, position, "FullTime"));
        var other = new Department("OTHER", "Other", true); db.Departments.Add(other); await db.SaveChangesAsync();
        var otherPosition = new Position("OTHER", "Other", true, other.DepartmentId); db.Positions.Add(otherPosition); await db.SaveChangesAsync();
        db.Employees.Add(new Employee("OUTSIDE", "Outside", "Employee", "outside@example.test", new(2026, 1, 1), "Active", other.DepartmentId, otherPosition.PositionId, "FullTime"));
        await db.SaveChangesAsync();
        for (var i = 1; i <= 6; i++) db.Departments.Add(new Department($"EXTRA{i}", $"Extra{i}", true));
        await db.SaveChangesAsync();
        await using var app = DisplayAndLoginApp(db);
        var password = "Aa1!" + Guid.NewGuid().ToString("N");
        await CreateDisplayAdmin(app, password);
        using var client = app.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Accept-Language", "en-US");
        using var login = await DisplayLogin(client, password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var url = $"/Employees?Search=PAG-&DepartmentId={department}";
        var first = await client.GetStringAsync(url);
        Assert.Equal(6, Regex.Matches(first, @"<td>PAG-\d{3}</td>").Count);
        Assert.Contains("Show all", first);
        Assert.Contains("Search=PAG-", first);
        var second = await client.GetStringAsync(url + "&Page=2");
        Assert.Contains("PAG-007", second); Assert.DoesNotContain("PAG-001</td>", second);
        var all = await client.GetStringAsync(url + "&ShowAll=true&Page=999");
        Assert.Equal(107, Regex.Matches(all, @"<td>PAG-\d{3}</td>").Count);
        Assert.Contains("Show 6 per page", all); Assert.DoesNotContain("OUTSIDE", all);
        var report = await client.GetStringAsync($"/Reports/Employees?DepartmentId={department}&ShowAll=true");
        Assert.Equal(107, Regex.Matches(report, @"<td>PAG-\d{3}</td>").Count);
        var limitedReport = await client.GetStringAsync($"/Reports/Employees?DepartmentId={department}");
        Assert.Equal(6, Regex.Matches(limitedReport, @"<td>PAG-\d{3}</td>").Count);
        var departments = await client.GetStringAsync("/Departments");
        Assert.Equal(6, Regex.Matches(Regex.Match(departments, @"<tbody>([\s\S]*?)</tbody>").Value, @"<tr\b").Count);
        var allDepartments = await client.GetStringAsync("/Departments?ShowAll=true");
        Assert.Equal(8, Regex.Matches(Regex.Match(allDepartments, @"<tbody>([\s\S]*?)</tbody>").Value, @"<tr\b").Count);
        await using var scope = app.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IEmployeeService>();
        var capped = await service.SearchAsync(new(null, department, null, null, false, 1, 1000));
        Assert.Equal(100, capped.Items.Count);
    });
}
