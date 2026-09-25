namespace LyricBuilder.Core.Models;

/// <summary>
/// The shape every catalogue slug must have, shared by the style and tag mutations.
/// </summary>
public static class SlugPattern
{
    public const string Value = "^[a-z0-9]+(-[a-z0-9]+)*$";

    public const string ErrorMessage =
        "slug must be lower-case letters and digits, separated by single hyphens";
}
