namespace LyricBuilder.Core.Models.Tags;

/// <summary>
/// Query-string filter for the tag list endpoint. Writers always get active tags.
/// </summary>
public class TagFilter : WithPagination
{
    public TagCategory? Category { get; set; }

    /// <summary>Matches anywhere in the name.</summary>
    public string? Name { get; set; }
}

/// <summary>
/// Query-string filter for the admin tag list, which also holds inactive tags.
/// </summary>
public class AdminTagFilter : TagFilter
{
    /// <summary>Omit to list active and inactive tags together.</summary>
    public bool? IsActive { get; set; }
}
