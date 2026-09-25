namespace LyricBuilder.Core.Models.Tags;

/// <summary>
/// Read model for a mood or theme tag.
/// </summary>
public class TagModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public TagCategory Category { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }

    public static TagModel Map(Tag tag) => new()
    {
        Id = tag.Id,
        Name = tag.Name,
        Slug = tag.Slug,
        Category = tag.Category,
        IsActive = tag.IsActive,
        CreatedAt = tag.CreatedAt
    };
}
