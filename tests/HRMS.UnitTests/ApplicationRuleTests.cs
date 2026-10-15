using System.Collections;
using System.Linq.Expressions;
using HRMS.Application.Auditing;
using HRMS.Application.Common;
using HRMS.Application.Employees;
using HRMS.Application.Identity;
using HRMS.Application.LeaveManagement;
using HRMS.Domain.Employees;
using HRMS.Domain.LeaveManagement;
using HRMS.Domain.Organization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HRMS.UnitTests;

// These tests exercise application decisions without SQL. Relational guarantees have separate SQL tests.
public class ApplicationRuleTests
{
    private static EmployeeInput Input => new("UNIT-1", "Test", "Employee", "unit@example.test", new(2026, 1, 1), "Active", "FullTime", 1, 1, null, null, null);
    private static T WithId<T>(T record, string property, int id) where T : class
    { typeof(T).GetProperty(property)!.SetValue(record, id); return record; }

    private static Mock<IEmployeeData> EmployeeData(params Employee[] employees)
    {
        var data = new Mock<IEmployeeData>();
        data.SetupGet(d => d.Employees).Returns(new MemoryRecords<Employee>(employees));
        data.SetupGet(d => d.Departments).Returns(new MemoryRecords<Department>([WithId(new Department("UNIT", "Unit", true), "DepartmentId", 1)]));
        data.SetupGet(d => d.Positions).Returns(new MemoryRecords<Position>([WithId(new Position("UNIT", "Unit", true, 1), "PositionId", 1)]));
        data.Setup(d => d.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IDataTransaction>());
        return data;
    }

    [Fact]
    public async Task DuplicateNumberIncludingDeletedEmployeeIsRejectedBeforeSaving()
    {
        var existing = new Employee("UNIT-1", "Old", "Employee", "old@example.test", new(2026, 1, 1), "Active", 1, 1, "FullTime");
        existing.SoftDelete(DateTime.UtcNow, null);
        var data = EmployeeData(existing);
        var result = await new EmployeeService(data.Object, Mock.Of<IAuditLogService>(), NullLogger<EmployeeService>.Instance).CreateAsync(Input);
        Assert.False(result.Succeeded);
        Assert.Contains(nameof(EmployeeInput.EmployeeNumber), result.Errors.Keys);
        data.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(999, 1, "DepartmentId")]
    [InlineData(1, 999, "PositionId")]
    public async Task InvalidAssignmentRejectedAtApplicationBoundary(int departmentId, int positionId, string field)
    {
        var data = EmployeeData();
        var result = await new EmployeeService(data.Object, Mock.Of<IAuditLogService>(), NullLogger<EmployeeService>.Instance)
            .CreateAsync(Input with { DepartmentId = departmentId, PositionId = positionId });
        Assert.False(result.Succeeded); Assert.Contains(field, result.Errors.Keys);
        data.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MissingRequiredEmployeeFieldsReturnErrorsInsteadOfPersisting()
    {
        var data = EmployeeData();
        var result = await new EmployeeService(data.Object, Mock.Of<IAuditLogService>(), NullLogger<EmployeeService>.Instance)
            .CreateAsync(Input with { FirstName = " ", HireDate = default });
        Assert.Contains(nameof(EmployeeInput.FirstName), result.Errors.Keys);
        Assert.Contains(nameof(EmployeeInput.HireDate), result.Errors.Keys);
        data.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmployeeCreationGeneratesAuditWithoutPersonalContactValues()
    {
        var audit = new Mock<IAuditLogService>(); var data = EmployeeData();
        var result = await new EmployeeService(data.Object, audit.Object, NullLogger<EmployeeService>.Instance).CreateAsync(Input);
        Assert.True(result.Succeeded);
        audit.Verify(a => a.RecordAsync(It.Is<AuditLogWriteRequest>(r => r.Action == "Employee.Created" &&
            r.EntityName == "Employee" && r.Changes.Any(c => c.Field == "EmployeeNumber" && c.After == null) &&
            !r.Changes.Any(c => c.After == Input.WorkEmail)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OverlappingPendingLeaveIsRejectedBeforeSave()
    {
        var data = new Mock<ILeaveData>();
        data.SetupGet(d => d.Employees).Returns(new MemoryRecords<Employee>([WithId(new Employee("UNIT-1", "Test", "Employee", "unit@example.test", new(2026, 1, 1), "Active", 1, 1, "FullTime"), "EmployeeId", 1)]));
        data.SetupGet(d => d.LeaveTypes).Returns(new MemoryRecords<LeaveType>([WithId(new LeaveType("Annual", "سنوية", 14), "LeaveTypeId", 1)]));
        data.SetupGet(d => d.LeaveRequests).Returns(new MemoryRecords<LeaveRequest>([new LeaveRequest(1, 1, new(2026, 7, 1), new(2026, 7, 3), "Existing")]));
        data.Setup(d => d.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IDataTransaction>());
        var access = new Mock<IRecordAccess>();
        access.Setup(a => a.CanAccessEmployeeAsync(1, false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var result = await new LeaveService(data.Object, Mock.Of<IAuditLogService>(), NullLogger<LeaveService>.Instance, access.Object)
            .SubmitLeaveRequestAsync(new(1, 1, new(2026, 7, 3), new(2026, 7, 5), "Overlap"));
        Assert.False(result.Succeeded);
        data.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnauthorizedApprovalDoesNotChangeRequestAndIsAudited()
    {
        var request = WithId(new LeaveRequest(1, 1, new(2026, 7, 1), new(2026, 7, 2), "Annual"), "LeaveRequestId", 1);
        var data = new Mock<ILeaveData>();
        data.SetupGet(d => d.LeaveRequests).Returns(new MemoryRecords<LeaveRequest>([request]));
        var access = new Mock<IRecordAccess>(); access.SetupGet(a => a.UserId).Returns("caller");
        access.Setup(a => a.CanAccessEmployeeAsync(1, true, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var audit = new Mock<IAuditLogService>();
        var result = await new LeaveService(data.Object, audit.Object, NullLogger<LeaveService>.Instance, access.Object).ApproveLeaveRequestAsync(1, "caller");
        Assert.False(result.Succeeded); Assert.Equal("Pending", request.Status);
        data.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        audit.Verify(a => a.RecordAsync(It.Is<AuditLogWriteRequest>(r => r.Action == "Authorization.LeaveReviewDenied" && r.Outcome == "Failure"), It.IsAny<CancellationToken>()), Times.Once);
    }
}

internal sealed class MemoryRecords<T>(IEnumerable<T> records) : MemoryQuery<T>(records.AsQueryable()), IMutableRecords<T> where T : class
{
    public void Add(T record) { }
}

internal class MemoryQuery<T> : IOrderedQueryable<T>
{
    private readonly IQueryable<T> query;
    public MemoryQuery(IQueryable<T> source) { query = source; Provider = new MemoryDriver(source.Provider); }
    public Type ElementType => typeof(T);
    public Expression Expression => query.Expression;
    public IQueryProvider Provider { get; }
    public IEnumerator<T> GetEnumerator() => query.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class MemoryDriver(IQueryProvider provider) : IQueryProvider, IQueryExecution
{
    public IQueryable CreateQuery(Expression e) => throw new NotSupportedException();
    public IQueryable<T> CreateQuery<T>(Expression e) => new MemoryQuery<T>(provider.CreateQuery<T>(e));
    public object? Execute(Expression e) => provider.Execute(e);
    public T Execute<T>(Expression e) => provider.Execute<T>(e);
    public IQueryable<T> ForRead<T>(Expression e) where T : class => CreateQuery<T>(e);
    public IQueryable<T> IncludingDeleted<T>(Expression e) where T : class => CreateQuery<T>(e);
    public Task<List<T>> ListAsync<T>(Expression e, CancellationToken ct) => Task.FromResult(provider.CreateQuery<T>(e).ToList());
    public Task<T?> SingleOrDefaultAsync<T>(Expression e, CancellationToken ct) => Task.FromResult(provider.CreateQuery<T>(e).SingleOrDefault());
    public Task<T> SingleAsync<T>(Expression e, CancellationToken ct) => Task.FromResult(provider.CreateQuery<T>(e).Single());
    public Task<T?> FirstOrDefaultAsync<T>(Expression e, CancellationToken ct) => Task.FromResult(provider.CreateQuery<T>(e).FirstOrDefault());
    public Task<bool> AnyAsync<T>(Expression e, CancellationToken ct) => Task.FromResult(provider.CreateQuery<T>(e).Any());
    public Task<int> CountAsync<T>(Expression e, CancellationToken ct) => Task.FromResult(provider.CreateQuery<T>(e).Count());
}
