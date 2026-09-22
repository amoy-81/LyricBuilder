using LyricBuilder.Domain.Enums;

namespace LyricBuilder.Domain.Entities;

/// <summary>
/// A song lyric authored on the platform — the sample aggregate this structure is built around.
/// </summary>
public class Lyric : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    /// <summary>The full lyric text, newline-separated by line.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Owning writer. Not a navigation yet — the identity model is not in place.</summary>
    public Guid AuthorId { get; set; }

    public LyricStatus Status { get; set; } = LyricStatus.Draft;

    /// <summary>Musical genre, free text until a lookup table exists.</summary>
    public string? Genre { get; set; }

    /// <summary>Written language as a BCP-47 tag (e.g. <c>fa-IR</c>, <c>en-US</c>).</summary>
    public string? Language { get; set; }

    /// <summary>Intended tempo in beats per minute, when the writer has one in mind.</summary>
    public int? Bpm { get; set; }

    public DateTime? PublishedAt { get; set; }
}
