using LyricBuilder.Core.Authentication;
using LyricBuilder.Core.Models.Accounts;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LyricBuilder.Core.Services.Accounts;

public interface ITokenService
{
    /// <summary>Issues an access token for <paramref name="account"/>, whose user must be loaded.</summary>
    AccessTokenModel CreateAccessToken(Account account);
}

/// <summary>
/// Signs access tokens with the key in <see cref="JwtOptions"/>. Validation lives with the
/// bearer scheme in <see cref="AuthenticationServiceCollectionExtensions.AddJwtAuthentication"/>.
/// </summary>
public sealed class TokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _jwt = options.Value;

    public AccessTokenModel CreateAccessToken(Account account)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_jwt.AccessTokenLifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(_jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [TokenClaims.Subject] = account.UserId.ToString(),
                [TokenClaims.Username] = account.Username,
                [TokenClaims.Name] = account.User.Name,
                [TokenClaims.Role] = account.Role.ToString()
            }
        };

        return new AccessTokenModel
        {
            AccessToken = new JsonWebTokenHandler().CreateToken(descriptor),
            ExpiresAt = expiresAt,
            UserId = account.UserId,
            Username = account.Username,
            Name = account.User.Name,
            Role = account.Role
        };
    }
}
