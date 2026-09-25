using System.ComponentModel.DataAnnotations;

namespace LyricBuilder.Core.Models.LyricSections;

/// <summary>
/// Write model for creating or updating a section. Deliberately omits Origin — the server
/// records who wrote the text, so a client cannot pass generated text off as human-written.
/// </summary>
public class LyricSectionMutation
{
    /// <summary>Nullable only so a missing value fails validation instead of defaulting to Intro.</summary>
    [Required]
    [EnumDataType(typeof(SectionType))]
    public SectionType? Type { get; init; }

    /// <summary>What this section should say — the input for generating it later.</summary>
    [MaxLength(1000)]
    public string? Brief { get; init; }

    /// <summary>The text, one line per line. Leave empty to write or generate it later.</summary>
    [MaxLength(4000)]
    public string? Content { get; init; }

    [MaxLength(32)]
    public string? RhymeScheme { get; init; }

    /// <summary>
    /// Zero-based place in the lyric. On create, omit to append at the end; on update, omit to
    /// leave the section where it is.
    /// </summary>
    public int? Position { get; init; }
}
