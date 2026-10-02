namespace LyricBuilder.Core.Models.ReferenceLyrics;

/// <summary>
/// Read model for a reference lyric, as admins curate it: the lyric fields plus attribution and
/// the tags generation ranks examples by.
/// </summary>
public class ReferenceLyricModel
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The sections composed into one text. Null on list responses, populated on single-item reads.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>The admin who curated it.</summary>
    public Guid AuthorId { get; init; }

    public Guid StyleId { get; init; }
    public string? Concept { get; init; }
    public string? Language { get; init; }
    public int? Bpm { get; init; }
    public string? OriginalArtist { get; init; }
    public IReadOnlyList<Guid> TagIds { get; init; } = [];
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Maps an entity to its read model. The lyric's tags must be loaded, and its sections too
    /// when <paramref name="includeContent"/> is true.
    /// </summary>
    public static ReferenceLyricModel Map(Lyric lyric, bool includeContent = true) => new()
    {
        Id = lyric.Id,
        Title = lyric.Title,
        Content = includeContent ? lyric.ComposeText() : null,
        AuthorId = lyric.AuthorId,
        StyleId = lyric.StyleId,
        Concept = lyric.Concept,
        Language = lyric.Language,
        Bpm = lyric.Bpm,
        OriginalArtist = lyric.OriginalArtist,
        TagIds = lyric.Tags.Select(t => t.Id).ToList(),
        CreatedAt = lyric.CreatedAt
    };
}
