namespace LyricBuilder.Core.Models.Lyrics;

/// <summary>
/// Read model for a lyric. Services return this rather than the entity, so persistence
/// concerns (soft-delete flags, audit columns) never reach a client.
/// </summary>
public class LyricModel
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The sections composed into one text. Null on list responses, populated on single-item reads.
    /// </summary>
    public string? Content { get; init; }

    public Guid AuthorId { get; init; }
    public LyricKind Kind { get; init; }
    public Guid StyleId { get; init; }
    public string? Concept { get; init; }
    public string? Language { get; init; }
    public int? Bpm { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Maps an entity to its read model. <paramref name="includeContent"/> keeps full lyric
    /// bodies out of list payloads; when true, the lyric's sections must be loaded.
    /// </summary>
    public static LyricModel Map(Lyric lyric, bool includeContent = true) => new()
    {
        Id = lyric.Id,
        Title = lyric.Title,
        Content = includeContent ? lyric.ComposeText() : null,
        AuthorId = lyric.AuthorId,
        Kind = lyric.Kind,
        StyleId = lyric.StyleId,
        Concept = lyric.Concept,
        Language = lyric.Language,
        Bpm = lyric.Bpm,
        CreatedAt = lyric.CreatedAt
    };
}
