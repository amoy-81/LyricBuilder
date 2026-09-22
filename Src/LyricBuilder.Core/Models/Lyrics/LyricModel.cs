namespace LyricBuilder.Core.Models.Lyrics;

/// <summary>
/// Read model for a lyric. Services return this rather than the entity, so persistence
/// concerns (soft-delete flags, audit columns) never reach a client.
/// </summary>
public class LyricModel
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;

    /// <summary>Null on list responses, populated on single-item reads.</summary>
    public string? Content { get; init; }

    public Guid AuthorId { get; init; }
    public LyricStatus Status { get; init; }
    public string? Genre { get; init; }
    public string? Language { get; init; }
    public int? Bpm { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? PublishedAt { get; init; }

    /// <summary>
    /// Maps an entity to its read model. <paramref name="includeContent"/> keeps full lyric
    /// bodies out of list payloads.
    /// </summary>
    public static LyricModel Map(Lyric lyric, bool includeContent = true) => new()
    {
        Id = lyric.Id,
        Title = lyric.Title,
        Content = includeContent ? lyric.Content : null,
        AuthorId = lyric.AuthorId,
        Status = lyric.Status,
        Genre = lyric.Genre,
        Language = lyric.Language,
        Bpm = lyric.Bpm,
        CreatedAt = lyric.CreatedAt,
        PublishedAt = lyric.PublishedAt
    };
}
