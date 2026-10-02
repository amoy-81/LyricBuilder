namespace LyricBuilder.Core.Models.ReferenceLyrics;

/// <summary>
/// Query-string filter for the admin list of reference lyrics.
/// </summary>
public class ReferenceLyricFilter : WithPagination
{
    public Guid? StyleId { get; set; }
    public string? Language { get; set; }

    /// <summary>Only references that carry this tag.</summary>
    public Guid? TagId { get; set; }

    /// <summary>Matches anywhere in the title.</summary>
    public string? Title { get; set; }

    /// <summary>Matches anywhere in the original artist's name.</summary>
    public string? OriginalArtist { get; set; }
}
