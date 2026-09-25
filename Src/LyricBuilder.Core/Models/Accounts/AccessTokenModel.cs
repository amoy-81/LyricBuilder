namespace LyricBuilder.Core.Models.Accounts;

/// <summary>
/// A signed access token plus who it was issued to, so a client can show the signed-in user
/// without decoding the token.
/// </summary>
public class AccessTokenModel
{
    /// <summary>Send as <c>Authorization: Bearer &lt;token&gt;</c>.</summary>
    public string AccessToken { get; init; } = string.Empty;

    public DateTime ExpiresAt { get; init; }

    public Guid UserId { get; init; }

    public string Username { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public AccountRole Role { get; init; }
}
