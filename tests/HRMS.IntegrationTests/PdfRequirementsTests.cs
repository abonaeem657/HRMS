using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using HRMS.Application.Common;
using HRMS.Application.Identity;
using HRMS.Application.LeaveManagement;
using HRMS.Application.Organization;
using HRMS.Domain.Employees;
using HRMS.Domain.LeaveManagement;
using HRMS.Domain.Organization;
using HRMS.Infrastructure.Auditing;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HRMS.IntegrationTests;

public partial class SqlWorkflowTests
{
    [SqlFact]
    public async Task ConflictingLeaveDecisionCannotOverwriteApprovedRequest() => await WithDatabase(async db =>
    {
        var (department, position) = await Organization(db);
        var reviewer = new HRMSUser { Id = "decision-reviewer", UserName = "reviewer@example.test", IsActive = true };
        db.Users.Add(reviewer);
        var employee = new Employee("DECISION", "Test", "Employee", "decision@example.test", new(2026, 1, 1), "Active", department, position, "FullTime");
        var type = new LeaveType("Decision", "اختبار القرار", 14);
        db.AddRange(employee, type); await db.SaveChangesAsync();
        var request = new LeaveRequest(employee.EmployeeId, type.LeaveTypeId, new(2027, 1, 1), new(2027, 1, 2), "Concurrent review");
        db.LeaveRequests.Add(request); await db.SaveChangesAsync();
        await using var second = new HRMSDbContext(new DbContextOptionsBuilder<HRMSDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
        // Both reviewers read the same version before the first decision commits.
        await second.LeaveRequests.SingleAsync(r => r.LeaveRequestId == request.LeaveRequestId);
        var access = new Mock<IRecordAccess>();
        access.SetupGet(a => a.UserId).Returns(reviewer.Id);
        access.Setup(a => a.CanAccessEmployeeAsync(employee.EmployeeId, true, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        LeaveService ServiceFor(HRMSDbContext context) => new(new ModuleData(context), new AuditLogService(context, new HttpContextAccessor()), NullLogger<LeaveService>.Instance, access.Object);
        Assert.True((await ServiceFor(db).ApproveLeaveRequestAsync(request.LeaveRequestId, reviewer.Id)).Succeeded);
        Assert.False((await ServiceFor(second).RejectLeaveRequestAsync(request.LeaveRequestId, reviewer.Id, "Stale decision")).Succeeded);
        db.ChangeTracker.Clear();
        var saved = await db.LeaveRequests.SingleAsync(r => r.LeaveRequestId == request.LeaveRequestId);
        Assert.Equal("Approved", saved.Status);
        Assert.Null(saved.ReviewNotes);
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "Leave.Approved"));
        Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "Leave.Rejected"));
    });

    private sealed class SqlReadCapture : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
        public void AssertPaged()
        {
            Assert.Contains(Commands, sql => sql.Contains("OFFSET") && sql.Contains("FETCH NEXT"));
            Commands.Clear();
        }
    }

    [SqlFact]
    public async Task ReferenceAccountLeaveAndBalancePagesAreLimitedBySqlBeforeMaterialization() => await WithDatabase(async db =>
    {
        var (departmentId, positionId) = await Organization(db);
        var employee = new Employee("PAGING", "Paging", "Employee", "paging@example.test", new(2026, 1, 1), "Active", departmentId, positionId, "FullTime");
        db.Employees.Add(employee);
        for (var i = 1; i <= 8; i++)
        {
            db.Departments.Add(new Department($"PAGE{i}", $"Department{i}", true));
            db.Positions.Add(new Position($"PAGE{i}", $"Position{i}", true, departmentId));
            var definition = i <= 3 ? LeavePolicy.Defaults[i - 1] : new LeaveTypeInput($"Legacy{i}", $"قديم{i}", 14, true, true);
            db.LeaveTypes.Add(new LeaveType(definition.Name, definition.NameAr, definition.DefaultDaysPerYear, definition.IsPaid));
            db.Users.Add(new HRMSUser { Id = $"paging-user-{i}", Email = $"user{i}@example.test", UserName = $"user{i}@example.test", NormalizedEmail = $"USER{i}@EXAMPLE.TEST", NormalizedUserName = $"USER{i}@EXAMPLE.TEST", IsActive = true });
        }
        await db.SaveChangesAsync();
        var typeIds = await db.LeaveTypes.Select(t => t.LeaveTypeId).ToListAsync();
        for (var i = 0; i < typeIds.Count; i++) db.LeaveRequests.Add(new LeaveRequest(employee.EmployeeId, typeIds[i], new(2026, 7, i + 1), new(2026, 7, i + 1), "Paging"));
        await db.SaveChangesAsync();
        var capture = new SqlReadCapture();
        await using var observed = new HRMSDbContext(new DbContextOptionsBuilder<HRMSDbContext>().UseSqlServer(db.Database.GetConnectionString()).AddInterceptors(capture).Options);
        var data = new ModuleData(observed);
        var audit = new AuditLogService(observed, new HttpContextAccessor());
        var departments = new DepartmentService(data, audit);
        var positions = new PositionService(data, audit);
        var accounts = new UserAccountService(data, Mock.Of<IIdentityStore>(), Mock.Of<HRMS.Application.Identity.IRoleStore>(), data, audit);
        var leave = new LeaveService(data, audit, NullLogger<LeaveService>.Instance, Mock.Of<IRecordAccess>());
        var page = new PageRequest(2, 6);
        var departmentPage = await departments.SearchAsync(new(null, null), page);
        Assert.Equal(9, departmentPage.TotalCount); Assert.Equal(3, departmentPage.Items.Count); capture.AssertPaged();
        var positionPage = await positions.SearchAsync(new(null, null, null), page);
        Assert.Equal(9, positionPage.TotalCount); Assert.Equal(3, positionPage.Items.Count); capture.AssertPaged();
        var sortedDepartments = await departments.SearchAsync(new(null, null, SortBy: "code", SortDescending: true), new(1, 6));
        Assert.Equal("TEST", sortedDepartments.Items[0].Code); capture.AssertPaged();
        var sortedPositions = await positions.SearchAsync(new(null, departmentId, null, "code", true), new(1, 6));
        Assert.Equal("TEST", sortedPositions.Items[0].Code); capture.AssertPaged();
        var accountPage = await accounts.SearchAccountsAsync(page);
        Assert.Equal(8, accountPage.TotalCount); Assert.Equal(2, accountPage.Items.Count);
        Assert.Equal(2, capture.Commands.Count); // count + one page query, no per-account role lookups
        capture.AssertPaged();
        var requests = await leave.SearchRequestsAsync(new(EmployeeId: employee.EmployeeId), page);
        Assert.Equal(8, requests.TotalCount); Assert.Equal(2, requests.Items.Count); capture.AssertPaged();
        var types = await leave.SearchTypesAsync(false, page);
        Assert.Equal(3, types.TotalCount); Assert.Equal(3, types.Items.Count); capture.AssertPaged();
        var balances = await leave.SearchBalancesAsync(employee.EmployeeId, 2026, page);
        Assert.Equal(3, balances.TotalCount); Assert.Equal(3, balances.Items.Count); Assert.All(balances.Items, b => Assert.Equal(1, b.PendingDays)); capture.AssertPaged();
        var all = await leave.SearchRequestsAsync(new(EmployeeId: employee.EmployeeId), new(ShowAll: true));
        Assert.Equal(8, all.Items.Count);
        await using var app = DisplayAndLoginApp(db);
        await using var scope = app.Services.CreateAsyncScope();
        var reporting = scope.ServiceProvider.GetRequiredService<HRMS.Application.Reporting.IReportingService>();
        var ascending = await reporting.GetLeaveReportAsync(new(EmployeeId: employee.EmployeeId, ShowAll: true, SortBy: "start", SortDescending: false));
        Assert.Equal(Enumerable.Range(1, 8), ascending.Items.Select(item => item.StartDate.Day));
        var descending = await reporting.GetLeaveReportAsync(new(EmployeeId: employee.EmployeeId, ShowAll: true, SortBy: "start", SortDescending: true));
        Assert.Equal(Enumerable.Range(1, 8).Reverse(), descending.Items.Select(item => item.StartDate.Day));
        var headcount = await reporting.GetDepartmentHeadcountPageAsync(null, new(1, 6), sortBy: "employees", sortDescending: true);
        Assert.Equal(departmentId, headcount.Items[0].DepartmentId);
        Assert.Equal(9, headcount.TotalCount);
    });
}
