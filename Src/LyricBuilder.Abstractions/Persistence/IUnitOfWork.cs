namespace LyricBuilder.Abstractions.Persistence;

/// <summary>
/// Commits everything staged through the repositories in one transaction.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
