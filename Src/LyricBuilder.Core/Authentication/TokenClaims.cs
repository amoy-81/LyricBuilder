using Microsoft.IdentityModel.JsonWebTokens;

namespace LyricBuilder.Core.Authentication;

/// <summary>
/// The claim names access tokens carry. Inbound claim mapping is off, so these are read back
/// exactly as written — no translation to the long <c>ClaimTypes</c> URIs.
/// </summary>
public static class TokenClaims
{
    /// <summary>The <see cref="Domain.Entities.User"/> id — what lyrics are authored under.</summary>
    public const string Subject = JwtRegisteredClaimNames.Sub;

    public const string Username = JwtRegisteredClaimNames.PreferredUsername;

    /// <summary>The user's display name.</summary>
    public const string Name = JwtRegisteredClaimNames.Name;

    public const string Role = "role";
}
