using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace LyricBuilder.Core.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Registers JWT bearer authentication as the default scheme. Throws at startup when the
    /// <c>Jwt</c> section is incomplete, so a misconfigured app fails before its first login.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(JwtOptions.SectionName);
        var jwt = section.Get<JwtOptions>() ?? new JwtOptions();

        if (jwt.Issuer.IsNullOrEmpty() || jwt.Audience.IsNullOrEmpty())
            throw new InvalidOperationException(
                $"{JwtOptions.SectionName}:Issuer and {JwtOptions.SectionName}:Audience must be configured.");

        if (jwt.CreateSigningKey().Key.Length < JwtOptions.MinSigningKeyBytes)
            throw new InvalidOperationException(
                $"{JwtOptions.SectionName}:SigningKey must be at least {JwtOptions.MinSigningKeyBytes} bytes.");

        services.Configure<JwtOptions>(section);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep claim names as issued (sub, role…) instead of mapping them to ClaimTypes URIs.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    NameClaimType = TokenClaims.Username,
                    RoleClaimType = TokenClaims.Role,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization();

        return services;
    }
}
