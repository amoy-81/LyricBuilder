using System.ComponentModel.DataAnnotations;

namespace LyricBuilder.Core.Models.Tags;

/// <summary>
/// Write model for creating or updating a tag. Admins only.
/// </summary>
public class TagMutation
{
    [Required]
    [MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Lower-case letters and digits separated by single hyphens, e.g. <c>city-life</c>. Unique
    /// within the category. Set on create; an update must send the same value.
    /// </summary>
    [Required]
    [MaxLength(100)]
    [RegularExpression(SlugPattern.Value, ErrorMessage = SlugPattern.ErrorMessage)]
    public string Slug { get; init; } = string.Empty;

    /// <summary>Nullable only so a missing value fails validation instead of defaulting to Mood.</summary>
    [Required]
    [EnumDataType(typeof(TagCategory))]
    public TagCategory? Category { get; init; }

    /// <summary>Clear to hide the tag from writers without deleting it.</summary>
    public bool IsActive { get; init; } = true;
}
