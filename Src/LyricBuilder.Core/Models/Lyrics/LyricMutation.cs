using System.ComponentModel.DataAnnotations;

namespace LyricBuilder.Core.Models.Lyrics;

/// <summary>
/// Write model for creating or updating a lyric's header. Deliberately omits AuthorId, Kind and
/// Status — those are server-owned, so a client cannot claim authorship, publish itself, or
/// pass its lyric off as a reference example. Text lives in sections, not here.
/// </summary>
public class LyricMutation
{
    [Required]
    [MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required]
    public Guid StyleId { get; init; }

    [MaxLength(2000)]
    public string? Concept { get; init; }

    [MaxLength(16)]
    public string? Language { get; init; }

    [Range(20, 400)]
    public int? Bpm { get; init; }
}
