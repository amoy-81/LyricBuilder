namespace LyricBuilder.Core.Models.Styles;

/// <summary>
/// Query-string filter for the style list endpoint. Writers always get active styles.
/// </summary>
public class StyleFilter : WithPagination
{
    /// <summary>Only the direct sub-styles of this style.</summary>
    public Guid? ParentStyleId { get; set; }

    /// <summary>Matches anywhere in the name.</summary>
    public string? Name { get; set; }
}

/// <summary>
/// Query-string filter for the admin style list, which also holds retired styles.
/// </summary>
public class AdminStyleFilter : StyleFilter
{
    /// <summary>Omit to list active and retired styles together.</summary>
    public bool? IsActive { get; set; }
}
