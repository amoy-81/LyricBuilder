using System.ComponentModel.DataAnnotations;

namespace LyricBuilder.Core.Models.ReferenceLyrics;

/// <summary>
/// Write model for creating or updating a reference lyric's header. Admins only. Omits AuthorId
/// and Kind, which the server sets; text lives in sections, not here.
/// </summary>
public class ReferenceLyricMutation
{
    [Required]
    [MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    /// <summary>Any style in the catalogue, retired ones included: their lyrics still generate.</summary>
    [Required]
    public Guid StyleId { get; init; }

    [MaxLength(2000)]
    public string? Concept { get; init; }

    /// <summary>
    /// BCP-47 tag, e.g. <c>fa-IR</c>. Required here, unlike on a writer's lyric: examples are
    /// matched by language, so a reference without one would never be picked.
    /// </summary>
    [Required]
    [MaxLength(16)]
    public string Language { get; init; } = string.Empty;

    [Range(20, 400)]
    public int? Bpm { get; init; }

    /// <summary>Performer of the original song, for attribution.</summary>
    [MaxLength(200)]
    public string? OriginalArtist { get; init; }

    /// <summary>
    /// The full set of moods and themes; an update replaces the old set. Inactive tags are
    /// allowed, since writers' existing lyrics may still carry them.
    /// </summary>
    [MaxLength(20)]
    public IReadOnlyList<Guid> TagIds { get; init; } = [];
}
