using System.ComponentModel.DataAnnotations;

namespace LyricBuilder.Core.Models.Lyrics;

/// <summary>
/// Write model for creating or updating a lyric. Deliberately omits AuthorId and Status —
/// those are server-owned, so a client cannot claim authorship or publish itself.
/// </summary>
public class LyricMutation
{
    [Required]
    [MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required]
    public string Content { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? Genre { get; init; }

    [MaxLength(16)]
    public string? Language { get; init; }

    [Range(20, 400)]
    public int? Bpm { get; init; }
}
