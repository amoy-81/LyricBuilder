namespace LyricBuilder.Core.Models.Styles;

/// <summary>
/// Read model for a style from the catalogue.
/// </summary>
public class StyleModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Description { get; init; }

    /// <summary>
    /// Instructions for the model. Null for writers — they are written for generation, not for
    /// people choosing a style — and populated for admins, who maintain them.
    /// </summary>
    public string? WritingGuidelines { get; init; }

    public Guid? ParentStyleId { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }

    public static StyleModel Map(Style style, bool includeGuidelines = false) => new()
    {
        Id = style.Id,
        Name = style.Name,
        Slug = style.Slug,
        Description = style.Description,
        WritingGuidelines = includeGuidelines ? style.WritingGuidelines : null,
        ParentStyleId = style.ParentStyleId,
        IsActive = style.IsActive,
        CreatedAt = style.CreatedAt
    };
}
