namespace LyricBuilder.Core.Models.Lyrics;

/// <summary>
/// Query-string filter for the lyric list endpoint.
/// </summary>
public class LyricFilter : WithPagination
{
    public LyricKind? Kind { get; set; }
    public Guid? StyleId { get; set; }
    public string? Language { get; set; }

    /// <summary>Case-insensitive match against the title.</summary>
    public string? Title { get; set; }
}
