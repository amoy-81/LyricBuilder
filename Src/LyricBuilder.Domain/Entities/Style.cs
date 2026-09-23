namespace LyricBuilder.Domain.Entities;

/// <summary>
/// A lyric style from the admin-curated catalogue — e.g. pop, social rap, traditional ballad.
/// </summary>
/// <remarks>
/// Every lyric has exactly one style, and it is the main key generation uses to find reference
/// examples. Styles nest: when a sub-style has too few references, generation can fall back to
/// those of its <see cref="ParentStyle"/>.
/// </remarks>
public class Style : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Stable, URL-safe key. Unlike <see cref="Name"/>, it does not change once set.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Shown to writers choosing a style.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Writing instructions for the model when generating in this style: vocabulary, rhyme
    /// density, line length, flow, typical imagery, what to avoid. Written for the model, not
    /// for writers.
    /// </summary>
    public string? WritingGuidelines { get; set; }

    public Guid? ParentStyleId { get; set; }

    public Style? ParentStyle { get; set; }

    public ICollection<Style> SubStyles { get; set; } = [];

    /// <summary>Inactive styles stay on existing lyrics but cannot be chosen for new ones.</summary>
    public bool IsActive { get; set; } = true;
}
