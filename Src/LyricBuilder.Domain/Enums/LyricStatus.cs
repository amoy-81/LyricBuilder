namespace LyricBuilder.Domain.Enums;

/// <summary>
/// Lifecycle of a lyric, from first draft to a version the writer is willing to publish.
/// </summary>
public enum LyricStatus
{
    Draft = 0,
    InReview = 1,
    Published = 2,
    Archived = 3
}
