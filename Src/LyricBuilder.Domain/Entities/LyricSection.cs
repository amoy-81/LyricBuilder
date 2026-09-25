using LyricBuilder.Abstractions.Domain.Exceptions;
using LyricBuilder.Domain.Enums;

namespace LyricBuilder.Domain.Entities;

/// <summary>
/// One part of a lyric — a verse, a chorus, a bridge — and the unit that gets written or
/// generated at a time.
/// </summary>
/// <remarks>
/// Order and repeats are structural, so <see cref="Position"/> and <see cref="RepeatsSectionId"/>
/// are set only by <see cref="Entities.Lyric"/>. Everything else is the section's own content.
/// </remarks>
public class LyricSection : BaseEntity
{
    public Guid LyricId { get; set; }

    public Lyric Lyric { get; set; } = null!;

    /// <summary>Zero-based place in the lyric. Contiguous across a lyric's sections.</summary>
    public int Position { get; internal set; }

    public SectionType Type { get; set; }

    /// <summary>What this section should say, in the writer's words — the input for generating it.</summary>
    public string? Brief { get; set; }

    /// <summary>
    /// The text, newline-separated by line. Empty until written or generated, and always empty
    /// on a repeat, whose text is its original's.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Rhyme pattern across the lines, e.g. <c>AABB</c> or <c>ABAB</c>.</summary>
    public string? RhymeScheme { get; set; }

    public ContentOrigin Origin { get; set; } = ContentOrigin.Human;

    /// <summary>
    /// Set when this section repeats an earlier one — typically the second and later choruses.
    /// Always points at the original, never at another repeat.
    /// </summary>
    public Guid? RepeatsSectionId { get; internal set; }

    public bool IsRepeat => RepeatsSectionId is not null;

    /// <summary>
    /// Replaces the text as written by a person. Generated text that a person changes becomes
    /// <see cref="ContentOrigin.AiEdited"/>, so it is not later mistaken for raw model output.
    /// </summary>
    public void EditContent(string content)
    {
        if (IsRepeat)
            throw LyricBuilderException.BadRequest(
                $"section {Id} is a repeat of {RepeatsSectionId}",
                "a repeated section takes its text from the original; edit that instead");

        if (content == Content)
            return;

        Content = content;
        if (Origin == ContentOrigin.AiGenerated)
            Origin = ContentOrigin.AiEdited;
    }
}
