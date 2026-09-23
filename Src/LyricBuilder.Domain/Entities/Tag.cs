using LyricBuilder.Domain.Enums;

namespace LyricBuilder.Domain.Entities;

/// <summary>
/// An admin-curated mood or theme. A lyric carries any number of them on top of its single
/// <see cref="Style"/>, and generation uses the overlap to pick the closest reference examples.
/// </summary>
public class Tag : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Stable, URL-safe key, unique within its <see cref="Category"/>.</summary>
    public string Slug { get; set; } = string.Empty;

    public TagCategory Category { get; set; }

    public ICollection<Lyric> Lyrics { get; set; } = [];
}
