using LyricBuilder.Domain.Enums;

namespace LyricBuilder.Domain.Entities;

/// <summary>
/// Sign-in credentials and role for one <see cref="Entities.User"/>.
/// </summary>
/// <remarks>
/// Authentication reads this; everything else reads the user. Access tokens carry the
/// <see cref="UserId"/> as their subject, because that is the id lyrics are authored under.
/// </remarks>
public class Account : BaseEntity
{
    /// <summary>Unique sign-in name, stored normalized (see <see cref="NormalizeUsername"/>).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Salted, versioned hash. The password itself is never stored.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public AccountRole Role { get; set; } = AccountRole.User;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>
    /// Usernames compare case-insensitively, so <c>Amir</c> and <c>amir</c> cannot both register.
    /// Normalize before storing and before every lookup.
    /// </summary>
    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();
}
