using System.Security.Claims;
using LyricBuilder.Core.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LyricBuilder.Core.Middlewares;

/// <summary>
/// Reads the authenticated principal into <see cref="RequestContext"/> for the rest of the pipeline.
/// </summary>
public class RequestContextMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context, RequestContext requestContext)
    {
        var user = context.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            if (Guid.TryParse(user.FindFirstValue(TokenClaims.Subject), out var userId))
                requestContext.UserId = userId;

            requestContext.UserName = user.Identity.Name;

            if (Enum.TryParse<AccountRole>(user.FindFirstValue(TokenClaims.Role), out var role))
                requestContext.Role = role;
        }

        await next(context);
    }
}

public static class RequestContextMiddlewareExtensions
{
    /// <summary>Must run after <c>UseAuthentication</c> — it reads the populated principal.</summary>
    public static IApplicationBuilder UseRequestContext(this IApplicationBuilder builder) =>
        builder.UseMiddleware<RequestContextMiddleware>();
}
