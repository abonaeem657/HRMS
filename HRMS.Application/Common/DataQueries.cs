using System.Linq.Expressions;
namespace HRMS.Application.Common.Queries;
public static class DataQueries
{
    private static IQueryExecution Driver<T>(IQueryable<T> source) => source.Provider as IQueryExecution
        ?? throw new InvalidOperationException("The query must originate from a configured module data port.");
    public static IQueryable<T> ForRead<T>(this IQueryable<T> q) where T : class => Driver(q).ForRead<T>(q.Expression);
    public static IQueryable<T> IncludingDeleted<T>(this IQueryable<T> q) where T : class => Driver(q).IncludingDeleted<T>(q.Expression);
    public static Task<List<T>> ToListAsync<T>(this IQueryable<T> q, CancellationToken ct = default) => Driver(q).ListAsync<T>(q.Expression, ct);
    public static Task<T?> SingleOrDefaultAsync<T>(this IQueryable<T> q, CancellationToken ct = default) => Driver(q).SingleOrDefaultAsync<T>(q.Expression, ct);
    public static Task<T?> SingleOrDefaultAsync<T>(this IQueryable<T> q, Expression<Func<T, bool>> predicate, CancellationToken ct = default) => q.Where(predicate).SingleOrDefaultAsync(ct);
    public static Task<T> SingleAsync<T>(this IQueryable<T> q, Expression<Func<T, bool>> predicate, CancellationToken ct = default) => Driver(q).SingleAsync<T>(q.Where(predicate).Expression, ct);
    public static Task<T?> FirstOrDefaultAsync<T>(this IQueryable<T> q, CancellationToken ct = default) => Driver(q).FirstOrDefaultAsync<T>(q.Expression, ct);
    public static Task<bool> AnyAsync<T>(this IQueryable<T> q, CancellationToken ct = default) => Driver(q).AnyAsync<T>(q.Expression, ct);
    public static Task<bool> AnyAsync<T>(this IQueryable<T> q, Expression<Func<T, bool>> predicate, CancellationToken ct = default) => q.Where(predicate).AnyAsync(ct);
    public static Task<int> CountAsync<T>(this IQueryable<T> q, CancellationToken ct = default) => Driver(q).CountAsync<T>(q.Expression, ct);
}
