namespace LyricBuilder.Core.Models.LyricSections;

/// <summary>
/// Read model for one section of a lyric.
/// </summary>
public class LyricSectionModel
{
    public Guid Id { get; init; }
    public Guid LyricId { get; init; }

    /// <summary>Zero-based place in the lyric. Contiguous across the lyric's sections.</summary>
    public int Position { get; init; }

    public SectionType Type { get; init; }
    public string? Brief { get; init; }

    /// <summary>Empty on a repeat — its text is the original's (see <see cref="RepeatsSectionId"/>).</summary>
    public string Content { get; init; } = string.Empty;

    public string? RhymeScheme { get; init; }
    public ContentOrigin Origin { get; init; }
    public Guid? RepeatsSectionId { get; init; }
    public DateTime CreatedAt { get; init; }

    public static LyricSectionModel Map(LyricSection section) => new()
    {
        Id = section.Id,
        LyricId = section.LyricId,
        Position = section.Position,
        Type = section.Type,
        Brief = section.Brief,
        Content = section.Content,
        RhymeScheme = section.RhymeScheme,
        Origin = section.Origin,
        RepeatsSectionId = section.RepeatsSectionId,
        CreatedAt = section.CreatedAt
    };
}
