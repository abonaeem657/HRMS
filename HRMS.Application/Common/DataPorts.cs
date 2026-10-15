using System.Data;
using System.Linq.Expressions;
using HRMS.Domain.Employees;
using HRMS.Domain.Organization;
using HRMS.Domain.Attendance;
using HRMS.Domain.LeaveManagement;

namespace HRMS.Application.Common;

// Provider-neutral, module-scoped ports. A module can mutate only its own records.
public interface IMutableRecords<T> : IQueryable<T> where T : class { void Add(T record); }
public interface IDataTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
public interface IDataSession
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDataTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<IDataTransaction> BeginTransactionAsync(IsolationLevel isolation, CancellationToken cancellationToken = default);
}




public class DataConcurrencyException(string message, Exception? inner = null) : Exception(message, inner);
public class DataWriteException(string message, bool isUnique, string? conflictCode, Exception inner) : Exception(message, inner)
{
    public bool IsUnique { get; } = isUnique;
    public string? ConflictCode { get; } = conflictCode;
}

// Async query execution is supplied by the persistence adapter, never by an EF reference here.
public interface IQueryExecution
{
    IQueryable<T> ForRead<T>(Expression expression) where T : class;
    IQueryable<T> IncludingDeleted<T>(Expression expression) where T : class;
    Task<List<T>> ListAsync<T>(Expression expression, CancellationToken cancellationToken);
    Task<T?> SingleOrDefaultAsync<T>(Expression expression, CancellationToken cancellationToken);
    Task<T> SingleAsync<T>(Expression expression, CancellationToken cancellationToken);
    Task<T?> FirstOrDefaultAsync<T>(Expression expression, CancellationToken cancellationToken);
    Task<bool> AnyAsync<T>(Expression expression, CancellationToken cancellationToken);
    Task<int> CountAsync<T>(Expression expression, CancellationToken cancellationToken);
}
