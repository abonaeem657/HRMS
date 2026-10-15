using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HRMS.Application.Identity;
using HRMS.Domain.Employees;
using HRMS.Domain.LeaveManagement;
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
using Microsoft.Playwright;

namespace HRMS.IntegrationTests;

public sealed class BrowserSqlFactAttribute : FactAttribute
{
    public BrowserSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HRMS_TEST_SQLSERVER"))) Skip = "Set HRMS_TEST_SQLSERVER for isolated browser acceptance.";
        else if (!File.Exists(Environment.GetEnvironmentVariable("HRMS_CHROME_PATH") ?? @"C:\Program Files\Google\Chrome\Application\chrome.exe")) Skip = "Chrome is unavailable; set HRMS_CHROME_PATH.";
    }
}

public partial class SqlWorkflowTests
{
    [BrowserSqlFact]
    public async Task SixRolesUseRealBrowserInBothLanguagesAndScreenSizesWithPublishedHttpsHost() => await WithDatabase(async db =>
    {
        var (department, position) = await Organization(db);
        var cs = db.Database.GetConnectionString()!;
        await using var seedHost = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(log => log.ClearProviders());
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:HRMS"] = cs }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<HRMSDbContext>>();
                services.AddDbContext<HRMSDbContext>(options => options.UseSqlServer(cs));
            });
        });
        var password = "Aa1!" + Guid.NewGuid().ToString("N");
        await using (var scope = seedHost.Services.CreateAsyncScope())
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
                var employee = new Employee(role.Name, "Browser", role.Name, email, new(2026, 1, 1), "Active", department, position, "FullTime", identityUserId: user.Id);
                db.Employees.Add(employee); await db.SaveChangesAsync();
                if (role.Name == IdentityRoles.DepartmentManager)
                {
                    (await db.Departments.SingleAsync()).AssignManager(employee.EmployeeId);
                    await db.SaveChangesAsync();
                }
            }
        }
        var spare = new Employee("SPARE", "Spare", "Employee", "spare@example.test", new(2026, 1, 1), "Active", department, position, "FullTime");
        var type = new LeaveType("Annual", "سنوية", 14);
        db.Employees.Add(spare); db.LeaveTypes.Add(type); await db.SaveChangesAsync();

        var sourceWeb = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../HRMS"));
        var published = Environment.GetEnvironmentVariable("HRMS_PUBLISHED_WEB_DLL");
        var dll = published ?? Path.Combine(sourceWeb, "bin/Release/net8.0/HRMS.dll");
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var address = $"https://localhost:{port}";
        var certFile = Path.Combine(Path.GetTempPath(), "hrms-acceptance-" + Guid.NewGuid().ToString("N") + ".pfx");
        var certPassword = Guid.NewGuid().ToString("N");
        using (var rsa = RSA.Create(2048))
        {
            var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder(); names.AddDnsName("localhost"); request.CertificateExtensions.Add(names.Build());
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
            await File.WriteAllBytesAsync(certFile, certificate.Export(X509ContentType.Pfx, certPassword));
        }
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = published is null ? sourceWeb : Path.GetDirectoryName(dll)!, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(dll); start.ArgumentList.Add("--urls"); start.ArgumentList.Add(address);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["ConnectionStrings__HRMS"] = cs;
        start.Environment["Identity__AllowInitialAdministratorSetup"] = "false";
        start.Environment["Logging__LogLevel__Default"] = "Warning";
        start.Environment["Kestrel__Certificates__Default__Path"] = certFile;
        start.Environment["Kestrel__Certificates__Default__Password"] = certPassword;
        using var host = Process.Start(start)!;
        host.BeginOutputReadLine(); host.BeginErrorReadLine();
        try
        {
            using var probe = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true });
            var ready = false;
            for (var attempt = 0; attempt < 100 && !host.HasExited; attempt++)
            {
                try { ready = (await probe.GetAsync(address + "/Account/Login")).IsSuccessStatusCode; if (ready) break; } catch (HttpRequestException) { }
                await Task.Delay(100);
            }
            Assert.True(ready, "Published HTTPS host did not start.");
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = Environment.GetEnvironmentVariable("HRMS_CHROME_PATH") ?? @"C:\Program Files\Google\Chrome\Application\chrome.exe", Headless = true });
            var errors = new List<string>();
            IBrowserContext? employeeContext = null;
            IPage? employeePage = null;
            foreach (var role in IdentityRoles.All)
            {
                var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, Locale = "en-US", ViewportSize = new() { Width = 1440, Height = 900 } });
                var page = await context.NewPageAsync(); page.PageError += (_, error) => errors.Add(error);
                await page.GotoAsync(address + "/Account/Login");
                await page.Locator("#Email").FillAsync(role.Name.ToLowerInvariant() + "@example.test");
                await page.Locator("#Password").FillAsync(password);
                await page.Locator("form button[type=submit]").ClickAsync();
                await page.WaitForURLAsync(address + "/"); await page.WaitForLoadStateAsync();
                foreach (var width in new[] { 1440, 390 })
                foreach (var culture in new[] { "en-US", "ar-JO" })
                {
                    await page.SetViewportSizeAsync(width, 900);
                    await page.GotoAsync(address + "/Home/SetLanguage?culture=" + culture + "&returnUrl=%2F");
                    Assert.Equal(culture == "ar-JO" ? "rtl" : "ltr", await page.Locator("html").GetAttributeAsync("dir"));
                    Assert.False(await page.Locator("#workspaceSidebar").IsVisibleAsync());
                    await page.EvaluateAsync("window.__acceptanceMarker = 'preserved'");
                    await page.Locator(".workspace-menu-bar button").ClickAsync();
                    await page.Locator("#workspaceSidebar.show").WaitForAsync();
                    await page.Locator(".main-nav a[href='/MyProfile']").ClickAsync();
                    await page.WaitForURLAsync("**/MyProfile");
                    Assert.Equal("preserved", await page.EvaluateAsync<string>("window.__acceptanceMarker"));
                    Assert.Contains(role.Name.ToLowerInvariant() + "@example.test", await page.Locator("#main-content").InnerTextAsync());
                    await page.Locator("#workspaceSidebar").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
                    await page.Locator(".workspace-menu-bar button").ClickAsync();
                    await page.Locator("#workspaceSidebar.show").WaitForAsync();
                    await page.Locator(".main-nav a[href='/Home/Guide']").ClickAsync();
                    await page.WaitForURLAsync("**/Home/Guide");
                    Assert.Equal(role.Name == IdentityRoles.SystemAdministrator, await page.Locator(".guide-grid a[href='/UserAccounts']").CountAsync() > 0);
                    Assert.Equal(role.Name != IdentityRoles.Employee, await page.Locator(".guide-grid a[href='/Reports']").CountAsync() > 0);
                    Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 2"), "Unexpected horizontal page overflow.");
                }
                if (role.Name == IdentityRoles.SystemAdministrator)
                {
                    await page.GotoAsync(address + "/Employees");
                    Assert.Equal(6, await page.Locator("#resultsContainer tbody tr").CountAsync());
                    await page.EvaluateAsync("window.__pagingMarker = 'preserved'");
                    await page.Locator(".list-pagination a[href*='Page=2']").ClickAsync();
                    await page.WaitForURLAsync("**/*Page=2*");
                    Assert.Equal(1, await page.Locator("#resultsContainer tbody tr").CountAsync());
                    Assert.Equal("preserved", await page.EvaluateAsync<string>("window.__pagingMarker"));
                    await page.Locator(".list-pagination a[href*='ShowAll=true']").ClickAsync();
                    await page.WaitForURLAsync("**/*ShowAll=true*");
                    Assert.Equal(7, await page.Locator("#resultsContainer tbody tr").CountAsync());
                    await page.Locator(".list-pagination a[href*='ShowAll=false']").ClickAsync();
                    await page.WaitForURLAsync("**/*ShowAll=false*");
                    Assert.Equal(6, await page.Locator("#resultsContainer tbody tr").CountAsync());
                    await page.GotoAsync(address + "/UserAccounts/Create");
                    await page.Locator("#Email").FillAsync("browser-created@example.test");
                    await page.Locator("#Password").FillAsync(password);
                    await page.Locator("#RoleName").SelectOptionAsync(IdentityRoles.Employee);
                    await page.Locator("#EmployeeId").SelectOptionAsync(spare.EmployeeId.ToString());
                    await page.Locator("#main-content form button[type=submit]").ClickAsync();
                    await page.WaitForURLAsync("**/UserAccounts");
                    Assert.Contains("browser-created@example.test", await page.Locator("#main-content").InnerTextAsync());
                }
                if (role.Name == IdentityRoles.Employee)
                {
                    employeeContext = context; employeePage = page;
                    await page.GotoAsync(address + "/LeaveManagement/Create");
                    await page.Locator("#LeaveTypeId").SelectOptionAsync(type.LeaveTypeId.ToString());
                    await page.Locator("#StartDate").FillAsync("2027-08-05");
                    await page.Locator("#EndDate").FillAsync("2027-08-06");
                    await page.Locator("#Reason").FillAsync("Browser acceptance leave");
                    Assert.Equal("", await page.Locator("#EndTime").InputValueAsync());
                    await page.Locator("#main-content form button[type=submit]").ClickAsync();
                    await page.WaitForURLAsync("**/LeaveManagement/MyReport");
                    await page.GotoAsync(address + "/Attendance");
                    for (var session = 0; session < 2; session++)
                    foreach (var action in new[] { "CheckIn", "CheckOut" })
                    {
                        await page.RunAndWaitForResponseAsync(() => page.Locator("form[action$='/" + action + "'] button").ClickAsync(), response => response.Url.EndsWith("/Attendance") && response.Request.Method == "GET");
                        await page.WaitForFunctionAsync("document.getElementById('main-content').getAttribute('aria-busy') !== 'true'");
                    }
                }
                else await context.CloseAsync();
            }
            // Review and correct through the HR interface after the employee submits real browser forms.
            await using var hrContext = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
            var hr = await hrContext.NewPageAsync(); hr.PageError += (_, error) => errors.Add(error);
            await hr.GotoAsync(address + "/Account/Login");
            await hr.Locator("#Email").FillAsync("hrmanager@example.test"); await hr.Locator("#Password").FillAsync(password);
            await hr.Locator("form button[type=submit]").ClickAsync(); await hr.WaitForURLAsync(address + "/");
            await hr.GotoAsync(address + "/LeaveManagement?status=Pending");
            await hr.Locator("form[action$='/Approve'] button").ClickAsync(); await hr.WaitForURLAsync("**/LeaveManagement");
            var record = await db.AttendanceRecords.AsNoTracking().OrderBy(r => r.AttendanceId).FirstAsync();
            await hr.GotoAsync(address + "/Attendance/Correct/" + record.AttendanceId);
            await hr.Locator("#Notes").FillAsync("Browser correction");
            await hr.Locator("#main-content form button[type=submit]").ClickAsync(); await hr.WaitForURLAsync("**/Attendance");
            await employeePage!.GotoAsync(address + "/LeaveManagement/MyReport?year=2027");
            var balanceCells = employeePage.Locator("section table tbody tr").First.Locator("td");
            Assert.Equal("2", await balanceCells.Nth(2).InnerTextAsync());
            Assert.Equal("0", await balanceCells.Nth(3).InnerTextAsync());
            Assert.Equal("12", await balanceCells.Nth(4).InnerTextAsync());
            db.ChangeTracker.Clear();
            Assert.Equal("Approved", (await db.LeaveRequests.SingleAsync()).Status);
            Assert.Equal(2, await db.AttendanceRecords.CountAsync());
            Assert.Equal("Browser correction", (await db.AttendanceRecords.FindAsync(record.AttendanceId))!.Notes);
            Assert.True(await db.Users.AnyAsync(u => u.Email == "browser-created@example.test"));
            Assert.Empty(errors);
            await employeeContext!.CloseAsync();
        }
        finally
        {
            if (!host.HasExited) { host.Kill(entireProcessTree: true); await host.WaitForExitAsync(); }
            File.Delete(certFile);
        }
    });
}
