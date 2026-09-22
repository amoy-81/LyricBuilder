using System.Linq.Expressions;

namespace LyricBuilder.Abstractions.Persistence;

/// <summary>
/// Persistence contract for a single entity type. Lives in Abstractions so services depend on
/// it without referencing EF Core.
/// </summary>
/// <remarks>
/// Writes stage changes only — nothing hits the database until <see cref="IUnitOfWork.SaveChangesAsync"/>
/// runs, so a service can compose several writes into one transaction.
/// </remarks>
public interface IRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

    Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken ct = default);

    /// <summary>One page of matches plus the total count ignoring paging.</summary>
    Task<(long TotalCount, IReadOnlyList<TEntity> Items)> ListPagedAsync(
        int skip,
        int take,
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken ct = default);

    Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default);

    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

    Task AddAsync(TEntity entity, CancellationToken ct = default);

    void Update(TEntity entity);

    /// <summary>Soft-deletes when the entity supports it; otherwise removes the row.</summary>
    void Remove(TEntity entity);

    /// <summary>Composable query for reads a method signature cannot express. Read-only (no tracking).</summary>
    IQueryable<TEntity> Query();
}
