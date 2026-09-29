using System.ComponentModel.DataAnnotations;

namespace LyricBuilder.Core.Models.LyricSections;

/// <summary>
/// Asks the AI to write or rework one section. What it does follows from the section:
/// <list type="bullet">
/// <item>No text yet — writes it from the brief, the song and the instruction.</item>
/// <item>Text and an instruction — revises the text as the instruction says.</item>
/// <item>Text and no instruction — polishes and completes the text.</item>
/// </list>
/// </summary>
public class SectionAiEditMutation
{
    /// <summary>
    /// What the writer wants, in their own words, e.g. "make it darker" or "write about the
    /// night we left". Optional.
    /// </summary>
    [MaxLength(1000)]
    public string? Instruction { get; init; }
}
