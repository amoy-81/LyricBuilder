namespace LyricBuilder.Domain.Enums;

/// <summary>
/// Separates writers' own lyrics from the admin-curated examples generation learns from. Both
/// share the same structure, so an example is queried exactly like any other lyric.
/// </summary>
public enum LyricKind
{
    /// <summary>A lyric a writer is building on the platform.</summary>
    Original = 0,

    /// <summary>An admin-curated sample, used as an example when generating sections.</summary>
    Reference = 1
}
