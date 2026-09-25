namespace LyricBuilder.Domain.Enums;

/// <summary>
/// What an account may do. Carried in the access token as a role claim, so endpoints check it
/// with <c>[Authorize(Roles = …)]</c> without a database read.
/// </summary>
public enum AccountRole
{
    /// <summary>A writer: builds their own lyrics. Every registration gets this role.</summary>
    User = 0,

    /// <summary>Curates the catalogue: styles, tags and the reference lyrics generation learns from.</summary>
    Admin = 1
}
