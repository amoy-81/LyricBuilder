using System.ComponentModel.DataAnnotations;

namespace LyricBuilder.Core.Models.Styles;

/// <summary>
/// Write model for creating or updating a style. Admins only.
/// </summary>
public class StyleMutation
{
    [Required]
    [MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Lower-case letters and digits separated by single hyphens, e.g. <c>social-rap</c>. Set on
    /// create; an update must send the same value, since slugs do not change.
    /// </summary>
    [Required]
    [MaxLength(100)]
    [RegularExpression(SlugPattern.Value, ErrorMessage = SlugPattern.ErrorMessage)]
    public string Slug { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    /// <summary>Instructions for the model when generating in this style. Not shown to writers.</summary>
    [MaxLength(8000)]
    public string? WritingGuidelines { get; init; }

    /// <summary>Makes this a sub-style. Must be an existing style, and not one of its own sub-styles.</summary>
    public Guid? ParentStyleId { get; init; }

    /// <summary>Clear to retire the style: existing lyrics keep it, new lyrics cannot choose it.</summary>
    public bool IsActive { get; init; } = true;
}
