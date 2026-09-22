namespace LyricBuilder.Domain.Entities;

/// <summary>
/// Root of every persisted entity: identity plus audit and soft-delete state.
/// </summary>
/// <remarks>
/// Timestamps hold UTC and are written by the persistence layer on save, not by callers, so
/// they stay consistent across every write path.
/// </remarks>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Soft-delete flag. Deleted rows stay in the table and are filtered out by a global query filter.</summary>
    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }
}
