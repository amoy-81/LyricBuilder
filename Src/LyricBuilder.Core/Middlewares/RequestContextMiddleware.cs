using System.Security.Claims;
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
            var subject = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");

            if (Guid.TryParse(subject, out var userId))
                requestContext.UserId = userId;

            requestContext.UserName = user.FindFirstValue(ClaimTypes.Name);
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
