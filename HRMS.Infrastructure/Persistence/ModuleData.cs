using HRMS.Application.Employees;
using HRMS.Application.Organization;
using HRMS.Application.Attendance;
using HRMS.Application.LeaveManagement;
using HRMS.Application.Identity;
using System.Collections;
using System.Data;
using System.Linq.Expressions;
using HRMS.Application.Common;
using HRMS.Domain.Employees;
using HRMS.Domain.Organization;
using HRMS.Domain.Attendance;
using HRMS.Domain.LeaveManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Data.SqlClient;

namespace HRMS.Infrastructure.Persistence;

/// <summary>Translates neutral module query/transaction ports into EF Core operations.</summary>
public sealed class ModuleData(HRMSDbContext db) : IEmployeeData, IOrganizationData, IAttendanceData, ILeaveData, IIdentityData, IEmployeeAccountLinks
{
    private static Query<T> Read<T>(IQueryable<T> query) => new(query, new QueryDriver(query.Provider));
    private Records<T> Write<T>(DbSet<T> set) where T : class => new(set, new QueryDriver(set.AsQueryable().Provider));
    IMutableRecords<Employee> IEmployeeData.Employees => Write(db.Employees);
    IQueryable<Employee> IOrganizationData.Employees => Read(db.Employees.AsNoTracking());
    IQueryable<Employee> IAttendanceData.Employees => Read(db.Employees.AsNoTracking());
    IQueryable<Employee> ILeaveData.Employees => Read(db.Employees.AsNoTracking());
    IQueryable<Employee> IIdentityData.Employees => Read(db.Employees.AsNoTracking());
    IQueryable<Department> IEmployeeData.Departments => Read(db.Departments.AsNoTracking());
    IQueryable<Position> IEmployeeData.Positions => Read(db.Positions.AsNoTracking());
    IMutableRecords<Department> IOrganizationData.Departments => Write(db.Departments);
    IMutableRecords<Position> IOrganizationData.Positions => Write(db.Positions);
    IMutableRecords<AttendanceRecord> IAttendanceData.AttendanceRecords => Write(db.AttendanceRecords);
    IMutableRecords<LeaveRequest> ILeaveData.LeaveRequests => Write(db.LeaveRequests);
    IMutableRecords<LeaveType> ILeaveData.LeaveTypes => Write(db.LeaveTypes);
    public IQueryable<AccountReference> Users => Read(db.Users.Select(u => new AccountReference { Id = u.Id, UserName = u.UserName, Email = u.Email, IsActive = u.IsActive }));
    public IQueryable<AccountRoleReference> UserRoles => Read(db.UserRoles.Select(r => new AccountRoleReference { UserId = r.UserId, RoleId = r.RoleId }));
    public IQueryable<RoleReference> Roles => Read(db.Roles.Select(r => new RoleReference { Id = r.Id, Name = r.Name }));
    public void ExpectVersion(Employee employee, byte[] version) => db.Entry(employee).Property(e => e.RowVersion).OriginalValue = version;
    public void ExpectVersion(AttendanceRecord record, byte[] version) => db.Entry(record).Property(e => e.RowVersion).OriginalValue = version;
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { return await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException error) { throw new DataConcurrencyException("A record changed. Reload before saving.", error); }
        catch (DbUpdateException error)
        {
            var unique = error.InnerException is SqlException { Number: 2601 or 2627 };
            var code = unique && error.InnerException!.Message.Contains("IX_Employees_EmployeeNumber", StringComparison.Ordinal) ? "EmployeeNumber" : null;
            throw new DataWriteException("The data operation could not be completed.", unique, code, error);
        }
    }
    public async Task<IDataTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => new Transaction(await db.Database.BeginTransactionAsync(cancellationToken));
    public async Task<IDataTransaction> BeginTransactionAsync(IsolationLevel isolation, CancellationToken cancellationToken = default) => new Transaction(await db.Database.BeginTransactionAsync(isolation, cancellationToken));
    public async Task SetAsync(string userId, int? employeeId, CancellationToken cancellationToken)
    {
        var current = await db.Employees.SingleOrDefaultAsync(e => e.IdentityUserId == userId, cancellationToken);
        var selected = employeeId.HasValue ? await db.Employees.SingleAsync(e => e.EmployeeId == employeeId.Value, cancellationToken) : null;
        if (current?.EmployeeId == selected?.EmployeeId) return;
        current?.LinkIdentityUser(null);
        selected?.LinkIdentityUser(userId);
        await SaveChangesAsync(cancellationToken);
    }
    private sealed class Transaction(IDbContextTransaction transaction) : IDataTransaction
    {
        public Task CommitAsync(CancellationToken ct = default) => transaction.CommitAsync(ct);
        public Task RollbackAsync(CancellationToken ct = default) => transaction.RollbackAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}

internal class Query<T>(IQueryable<T> query, QueryDriver driver) : IOrderedQueryable<T>
{
    public Type ElementType => typeof(T);
    public Expression Expression => query.Expression;
    public IQueryProvider Provider => driver;
    public IEnumerator<T> GetEnumerator() => query.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
internal sealed class Records<T>(DbSet<T> set, QueryDriver driver) : Query<T>(set, driver), IMutableRecords<T> where T : class
{
    public void Add(T record) => set.Add(record);
}
internal sealed class QueryDriver(IQueryProvider provider) : IQueryProvider, IQueryExecution
{
    public IQueryable CreateQuery(Expression expression)
    {
        var query = provider.CreateQuery(expression);
        return (IQueryable)Activator.CreateInstance(typeof(Query<>).MakeGenericType(query.ElementType), query, this)!;
    }
    public IQueryable<T> CreateQuery<T>(Expression expression) => new Query<T>(provider.CreateQuery<T>(expression), this);
    public object? Execute(Expression expression) => provider.Execute(expression);
    public T Execute<T>(Expression expression) => provider.Execute<T>(expression);
    public IQueryable<T> ForRead<T>(Expression expression) where T : class => new Query<T>(provider.CreateQuery<T>(expression).AsNoTracking(), this);
    public IQueryable<T> IncludingDeleted<T>(Expression expression) where T : class => new Query<T>(provider.CreateQuery<T>(expression).IgnoreQueryFilters(), this);
    public Task<List<T>> ListAsync<T>(Expression expression, CancellationToken ct) => provider.CreateQuery<T>(expression).ToListAsync(ct);
    public Task<T?> SingleOrDefaultAsync<T>(Expression expression, CancellationToken ct) => provider.CreateQuery<T>(expression).SingleOrDefaultAsync(ct);
    public Task<T> SingleAsync<T>(Expression expression, CancellationToken ct) => provider.CreateQuery<T>(expression).SingleAsync(ct);
    public Task<T?> FirstOrDefaultAsync<T>(Expression expression, CancellationToken ct) => provider.CreateQuery<T>(expression).FirstOrDefaultAsync(ct);
    public Task<bool> AnyAsync<T>(Expression expression, CancellationToken ct) => provider.CreateQuery<T>(expression).AnyAsync(ct);
    public Task<int> CountAsync<T>(Expression expression, CancellationToken ct) => provider.CreateQuery<T>(expression).CountAsync(ct);
}
