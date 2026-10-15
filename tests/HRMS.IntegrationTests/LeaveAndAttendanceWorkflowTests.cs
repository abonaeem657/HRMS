using HRMS.Application.Attendance;
using HRMS.Application.Identity;
using HRMS.Application.LeaveManagement;
using HRMS.Domain.Employees;
using HRMS.Domain.LeaveManagement;
using HRMS.Infrastructure.Auditing;
using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HRMS.IntegrationTests;

public partial class SqlWorkflowTests
{
    [SqlFact]
    public async Task ForeignModuleReadsCannotPersistEmployeeChangesThroughTheirSession() => await WithDatabase(async db =>
    {
        var (department, position) = await Organization(db);
        db.Employees.Add(new Employee("DETACHED", "Read", "Only", "readonly@example.test", new(2026, 1, 1), "Active", department, position, "FullTime"));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        HRMS.Application.LeaveManagement.ILeaveData leavePort = new HRMS.Infrastructure.Persistence.ModuleData(db);
        var employee = Assert.Single(await HRMS.Application.Common.Queries.DataQueries.ToListAsync(leavePort.Employees));
        employee.SoftDelete(DateTime.UtcNow, null);
        await leavePort.SaveChangesAsync();
        Assert.False((await db.Employees.SingleAsync()).IsDeleted);
    });
    [SqlFact]
    public async Task PendingDaysAreReservedApprovedOnceAndReleasedOnRejectionOrCancellation() => await WithDatabase(async db =>
    {
        var (department, position) = await Organization(db);
        var owner = new HRMSUser { Id = Guid.NewGuid().ToString(), UserName = "owner@example.test", Email = "owner@example.test", IsActive = true };
        db.Users.Add(owner);
        var employee = new Employee("L1", "Leave", "Owner", "owner@example.test", new(2026, 1, 1), "Active", department, position, "FullTime", identityUserId: owner.Id);
        var type = new LeaveType("Annual", "سنوية", 14);
        db.Employees.Add(employee);
        db.LeaveTypes.Add(type);
        await db.SaveChangesAsync();
        var employeeId = employee.EmployeeId;
        var typeId = type.LeaveTypeId;
        var access = new Mock<IRecordAccess>();
        access.SetupGet(a => a.UserId).Returns(owner.Id);
        access.Setup(a => a.CanAccessEmployeeAsync(employeeId, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var service = new LeaveService(new HRMS.Infrastructure.Persistence.ModuleData(db), new AuditLogService(db, new HttpContextAccessor()), NullLogger<LeaveService>.Instance, access.Object);
        async Task AssertBalance(int used, int pending, int remaining, int year = 2027)
        {
            db.ChangeTracker.Clear();
            var balance = Assert.Single(await service.GetEmployeeLeaveBalancesAsync(employeeId, year));
            Assert.Equal((used, pending, remaining), (balance.UsedDays, balance.PendingDays, balance.RemainingDays));
        }
        await AssertBalance(0, 0, 14);
        // A prefilled start time must not require an end time for a day-based request.
        var submitted = await service.SubmitLeaveRequestAsync(new(employeeId, typeId, new(2027, 1, 10), new(2027, 1, 11), "Annual", new(9, 30), null));
        Assert.True(submitted.Succeeded);
        await AssertBalance(0, 2, 12);
        var saved = await service.GetLeaveRequestByIdAsync(submitted.LeaveRequestId!.Value);
        Assert.Equal(new TimeOnly(9, 30), saved!.StartTime);
        Assert.Null(saved.EndTime);
        Assert.True((await service.ApproveLeaveRequestAsync(saved.LeaveRequestId, owner.Id)).Succeeded);
        await AssertBalance(2, 0, 12);
        Assert.False((await service.ApproveLeaveRequestAsync(saved.LeaveRequestId, owner.Id)).Succeeded);
        await AssertBalance(2, 0, 12);
        var rejected = await service.SubmitLeaveRequestAsync(new(employeeId, typeId, new(2027, 2, 10), new(2027, 2, 12), "Reject"));
        Assert.True(rejected.Succeeded);
        await AssertBalance(2, 3, 9);
        Assert.True((await service.RejectLeaveRequestAsync(rejected.LeaveRequestId!.Value, owner.Id, "Not available")).Succeeded);
        await AssertBalance(2, 0, 12);
        var cancelled = await service.SubmitLeaveRequestAsync(new(employeeId, typeId, new(2027, 3, 10), new(2027, 3, 10), "Cancel"));
        Assert.True(cancelled.Succeeded);
        await AssertBalance(2, 1, 11);
        Assert.True((await service.CancelLeaveRequestAsync(cancelled.LeaveRequestId!.Value, owner.Id)).Succeeded);
        await AssertBalance(2, 0, 12);
        var crossYear = await service.SubmitLeaveRequestAsync(new(employeeId, typeId, new(2027, 12, 31), new(2028, 1, 2), "Cross year"));
        Assert.True(crossYear.Succeeded);
        await AssertBalance(2, 1, 11);
        await AssertBalance(0, 2, 12, 2028);
    });

    [SqlFact]
    public async Task MultipleDailySessionsPersistAndCorrectionTargetsOnlyTheSelectedSession() => await WithDatabase(async db =>
    {
        var (department, position) = await Organization(db);
        var employee = new Employee("A1", "Attendance", "Owner", "attendance@example.test", new(2026, 1, 1), "Active", department, position, "FullTime");
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        var service = new AttendanceService(new HRMS.Infrastructure.Persistence.ModuleData(db), new AuditLogService(db, new HttpContextAccessor()), new ConfigurationBuilder().Build(), NullLogger<AttendanceService>.Instance);
        var start = DateTime.UtcNow.Date.AddDays(-1).AddHours(5);
        var day = DateOnly.FromDateTime(start.AddHours(3));
        var first = await service.CheckInAsync(employee.EmployeeId, start);
        Assert.True(first.Succeeded);
        Assert.False((await service.CheckInAsync(employee.EmployeeId, start.AddMinutes(1))).Succeeded);
        Assert.True((await service.CheckOutAsync(employee.EmployeeId, start.AddHours(1))).Succeeded);
        var second = await service.CheckInAsync(employee.EmployeeId, start.AddHours(2));
        Assert.True(second.Succeeded);
        Assert.True((await service.CheckOutAsync(employee.EmployeeId, start.AddHours(3))).Succeeded);
        db.ChangeTracker.Clear();
        var filter = new AttendanceFilter(EmployeeId: employee.EmployeeId, FromDate: day, ToDate: day);
        Assert.Equal(2, (await service.GetAttendanceListAsync(filter)).TotalCount);
        Assert.Equal(2m, (await service.GetSummaryAsync(filter)).TotalHoursWorked);
        // Selecting just a day is ambiguous when it has several sessions.
        Assert.False((await service.RecordManualEntryAsync(employee.EmployeeId, day, start, start.AddHours(1), "Present", null)).Succeeded);
        var firstVersion = (await service.GetByIdAsync(first.AttendanceId!.Value))!.RowVersion;
        var secondVersion = (await service.GetByIdAsync(second.AttendanceId!.Value))!.RowVersion;
        Assert.True((await service.RecordManualEntryAsync(employee.EmployeeId, day, start, start.AddMinutes(45), "Present", "Corrected", attendanceId: first.AttendanceId, rowVersion: firstVersion)).Succeeded);
        Assert.False((await service.RecordManualEntryAsync(employee.EmployeeId, day, start, start.AddMinutes(30), "Present", "Stale", attendanceId: first.AttendanceId, rowVersion: firstVersion)).Succeeded);
        db.ChangeTracker.Clear();
        Assert.Equal(.75m, (await service.GetByIdAsync(first.AttendanceId!.Value))!.TotalHours);
        Assert.Equal(1m, (await service.GetByIdAsync(second.AttendanceId!.Value))!.TotalHours);
        Assert.False((await service.RecordManualEntryAsync(employee.EmployeeId, day, start.AddMinutes(30), start.AddHours(3), "Present", null, attendanceId: second.AttendanceId, rowVersion: secondVersion)).Succeeded);
        Assert.Equal(1.75m, (await service.GetSummaryAsync(filter)).TotalHoursWorked);
    });
}
