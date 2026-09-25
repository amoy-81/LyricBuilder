using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace LyricBuilder.Core.Authentication;

/// <summary>
/// Bound from the <c>Jwt</c> configuration section. The same values sign tokens in
/// <see cref="Services.Accounts.TokenService"/> and validate them on every request.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 needs a key of at least 256 bits.</summary>
    public const int MinSigningKeyBytes = 32;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    /// <summary>Shared secret. Keep it out of source control outside development.</summary>
    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenLifetimeMinutes { get; init; } = 60;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
