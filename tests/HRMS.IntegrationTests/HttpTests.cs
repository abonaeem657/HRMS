using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using HRMS.Application.Auditing;
using HRMS.Application.Employees;
using HRMS.Application.Identity;
using HRMS.Application.External;
using HRMS.Application.LeaveManagement;
using HRMS.Application.Organization;
using HRMS.Application.Reporting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
namespace HRMS.IntegrationTests;
public class HttpFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:HRMS"]="Server=localhost;Database=HRMS_HttpTests;Integrated Security=True;TrustServerCertificate=True", ["Identity:AllowInitialAdministratorSetup"]="false" }));
        builder.ConfigureServices(services=>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddAuthentication(o=> {o.DefaultAuthenticateScheme="Test";o.DefaultChallengeScheme="Test";o.DefaultForbidScheme="Test";}).AddScheme<AuthenticationSchemeOptions,TestAuth>("Test",_=>{});
            var employees=new Mock<IEmployeeService>();
            employees.Setup(x=>x.SearchAsync(It.IsAny<EmployeeListQuery>(),It.IsAny<CancellationToken>())).ReturnsAsync(new EmployeeListPage([],0,1,25));
            services.AddSingleton(employees.Object);
            services.AddSingleton(Mock.Of<IUserAccountService>());
            services.AddSingleton(Mock.Of<IAuditLogService>());
            var external=new Mock<IPublicHolidaysService>();external.Setup(x=>x.GetHolidaysAsync(It.IsAny<int>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new TaskCanceledException());services.AddSingleton(external.Object);
            var leaves=new Mock<ILeaveService>();
            leaves.Setup(x=>x.GetLeaveRequestByIdAsync(42,It.IsAny<CancellationToken>())).ReturnsAsync(new LeaveRequestDto(42,99,"99","Other",2,"Other",1,"Annual","سنوية",new(2026,1,1),new(2026,1,2),2,"Private","Pending",DateTime.UtcNow,null,null,null,null));
            services.AddSingleton(leaves.Object);
        });
    }
}
public sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role=Request.Headers["X-Test-Role"].ToString();
        if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
        var user=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"test-user"),new Claim(ClaimTypes.Name,"test"),new Claim(ClaimTypes.Role,role)},"Test"));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(user,"Test")));
    }
}
public class HttpTests : IClassFixture<HttpFactory>
{
    [Theory]
    [InlineData("SystemAdministrator", "ar-JO")]
    [InlineData("SystemAdministrator", "en-US")]
    [InlineData("HRManager", "ar-JO")]
    [InlineData("HROfficer", "en-US")]
    [InlineData("DepartmentManager", "ar-JO")]
    [InlineData("Employee", "ar-JO")]
    [InlineData("Employee", "en-US")]
    [InlineData("Auditor", "en-US")]
    public async Task WorkspaceRendersHomeAndGuideWithRoleNavigation(string role, string culture)
    {
        using var preview = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            var reporting = new Mock<IReportingService>();
            reporting.Setup(x => x.GetDashboardAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DashboardSummary(12, [], 0, new Dictionary<string, int>(), 2, new(0, 0, 0, 0, 0, 0), []));
            services.AddSingleton(reporting.Object);
            var departments = new Mock<IDepartmentService>();
            departments.Setup(x => x.GetListAsync(It.IsAny<DepartmentListQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DepartmentListItem> { new(1, "IT", "IT", null, 12, true) });
            services.AddSingleton(departments.Object);
            var positions = new Mock<IPositionService>();
            positions.Setup(x => x.GetListAsync(It.IsAny<PositionListQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PositionListItem> { new(1, "DEV", "Developer", "IT", 12, true, null) });
            services.AddSingleton(positions.Object);
            var employees = new Mock<IEmployeeService>();
            employees.Setup(x => x.SearchAsync(It.IsAny<EmployeeListQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EmployeeListPage([], 12, 1, 25));
            services.AddSingleton(employees.Object);
            var accounts = new Mock<IUserAccountService>();
            accounts.Setup(x => x.GetAccountsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<UserAccountSummary>());
            services.AddSingleton(accounts.Object);
        }));
        using var client = preview.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("Accept-Language", culture);
        var html = await client.GetStringAsync("/");
        Assert.Contains("home-simple", html);
        Assert.Contains("workspace-layout", html);
        Assert.Contains("href=\"/Home/Guide\"", html);
        Assert.Equal(role == "SystemAdministrator", html.Contains("href=\"/UserAccounts\""));
        Assert.Equal(role is "SystemAdministrator" or "HRManager" or "Auditor", html.Contains("href=\"/AuditLogs\""));
        Assert.DoesNotContain("setup-steps", html);
        var guide = await client.GetStringAsync("/Home/Guide");
        Assert.Contains("guide-step", guide);
        Assert.Contains("href=\"/Account/ChangePassword\"", guide);
        // Count task links in the guide, excluding the shared navigation.
        var guideTasks = guide[guide.IndexOf("<div class=\"guide-grid\">", StringComparison.Ordinal)..];
        Assert.Equal(role == "SystemAdministrator", guideTasks.Contains("href=\"/UserAccounts\""));
        Assert.Equal(role is "SystemAdministrator" or "HRManager" or "HROfficer", guideTasks.Contains("href=\"/Employees\""));
        Assert.Equal(role != "Employee", guideTasks.Contains("href=\"/Reports\""));
        Assert.Equal(role != "Auditor", guideTasks.Contains("href=\"/LeaveManagement/MyReport\""));
        Assert.Equal(role is "SystemAdministrator" or "HRManager" or "Auditor", guideTasks.Contains("href=\"/AuditLogs\""));
        var output = Environment.GetEnvironmentVariable("HRMS_UI_PREVIEW_DIR");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, $"home-{role}-{culture}.html"), html);
            await File.WriteAllTextAsync(Path.Combine(output, $"guide-{culture}.html"), guide);
        }
    }

    private readonly HttpFactory factory;
    public HttpTests(HttpFactory factory)=>this.factory=factory;
    private HttpClient Client(string? role=null)
    {var c=factory.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});if(role!=null)c.DefaultRequestHeaders.Add("X-Test-Role",role);return c;}
    [Fact] public async Task ApiRequiresLogin(){using var c=Client();var response=await c.GetAsync("/api/v1/employees");Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);Assert.Equal("application/problem+json",response.Content.Headers.ContentType?.MediaType);Assert.Contains("401",await response.Content.ReadAsStringAsync());}
    [Theory][InlineData("Employee")][InlineData("DepartmentManager")][InlineData("Auditor")]
    public async Task EmployeeDirectoryRejectsUnauthorizedRoles(string role){using var c=Client(role);Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync("/api/v1/employees")).StatusCode);}
    [Theory][InlineData("SystemAdministrator")][InlineData("HRManager")][InlineData("HROfficer")]
    public async Task EmployeeDirectoryAllowsHr(string role){using var c=Client(role);Assert.Equal(HttpStatusCode.OK,(await c.GetAsync("/api/v1/employees")).StatusCode);}
    [Fact] public async Task UnknownEmployeeReturns404(){using var c=Client("HRManager");Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync("/api/v1/employees/999")).StatusCode);}
    [Fact] public async Task EmployeeCannotReadOtherLeave(){using var c=Client("Employee");Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync("/api/v1/leave-requests/42")).StatusCode);}
    [Fact] public async Task HrOfficerCannotOpenAudit(){using var c=Client("HROfficer");Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync("/AuditLogs")).StatusCode);}
    [Fact] public async Task ExternalTimeoutReturns504(){using var c=Client("HRManager");Assert.Equal(HttpStatusCode.GatewayTimeout,(await c.GetAsync("/api/v1/public-holidays/2026")).StatusCode);}
    [Fact] public async Task MissingHireDateReturns400()
    {
        using var c=Client("HRManager");var token=await c.GetFromJsonAsync<Dictionary<string,string>>("/api/v1/antiforgery");c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",token!["requestToken"]);
        var response=await c.PostAsJsonAsync("/api/v1/employees",new{employeeNumber="1",firstName="A",lastName="B",workEmail="a@example.test",employmentStatus="Active",employmentType="FullTime",departmentId=1,positionId=1});
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);Assert.Contains("HireDate",await response.Content.ReadAsStringAsync());
    }
    [Fact] public async Task MutationsRequireAntiforgery(){using var c=Client("HRManager");Assert.Equal(HttpStatusCode.BadRequest,(await c.DeleteAsync("/api/v1/employees/1")).StatusCode);}
    [Theory][InlineData("en-US","ltr")][InlineData("ar-JO","rtl")]
    public async Task LoginRendersRequestedCulture(string culture,string direction){using var c=Client();c.DefaultRequestHeaders.Add("Accept-Language",culture);var text=await c.GetStringAsync("/Account/Login");Assert.Contains($"dir=\"{direction}\"",text);Assert.Contains($"lang=\"{culture}\"",text);}
    [Fact] public async Task OpenApiDocumentAvailableInDevelopment(){using var c=Client();Assert.Equal(HttpStatusCode.OK,(await c.GetAsync("/swagger/v1/swagger.json")).StatusCode);}
    [Fact] public async Task ApiRateLimitReturns429()
    {
        using var isolatedFactory = new HttpFactory();
        using var client = isolatedFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        for (var attempt = 0; attempt < 120; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/employees")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await client.GetAsync("/api/v1/employees")).StatusCode);
    }
}
