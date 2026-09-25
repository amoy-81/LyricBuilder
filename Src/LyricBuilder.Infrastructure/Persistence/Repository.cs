using System.Linq.Expressions;
using LyricBuilder.Abstractions.Persistence;
using LyricBuilder.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LyricBuilder.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IRepository{TEntity}"/>, registered open-generic so
/// every entity gets one without a per-type class.
/// </summary>
public class Repository<TEntity>(LyricBuilderDbContext dbContext) : IRepository<TEntity>
    where TEntity : class
{
    private readonly DbSet<TEntity> _set = dbContext.Set<TEntity>();

    public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _set.FindAsync([id], ct);

    public async Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default) =>
        await _set.FirstOrDefaultAsync(predicate, ct);

    public async Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken ct = default) =>
        await Filtered(predicate).AsNoTracking().ToListAsync(ct);

    public async Task<(long TotalCount, IReadOnlyList<TEntity> Items)> ListPagedAsync(
        int skip,
        int take,
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        var query = Filtered(predicate).AsNoTracking();

        var total = await query.LongCountAsync(ct);
        var items = await query.Skip(skip).Take(take).ToListAsync(ct);

        return (total, items);
    }

    public async Task<long> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken ct = default) =>
        await Filtered(predicate).LongCountAsync(ct);

    public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default) =>
        await _set.AnyAsync(predicate, ct);

    public async Task AddAsync(TEntity entity, CancellationToken ct = default) =>
        await _set.AddAsync(entity, ct);

    public void Update(TEntity entity) => _set.Update(entity);

    public void Remove(TEntity entity)
    {
        if (entity is BaseEntity auditable)
        {
            auditable.IsDeleted = true;
            auditable.DeletedAt = DateTime.UtcNow;
            _set.Update(entity);
            return;
        }

        _set.Remove(entity);
    }

    public IQueryable<TEntity> Query() => _set.AsNoTracking();

    public IQueryable<TEntity> QueryTracked() => _set;

    private IQueryable<TEntity> Filtered(Expression<Func<TEntity, bool>>? predicate) =>
        predicate is null ? _set : _set.Where(predicate);
}
