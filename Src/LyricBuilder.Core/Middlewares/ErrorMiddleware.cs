using System.Net;
using LyricBuilder.Abstractions.Domain.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LyricBuilder.Core.Middlewares;

/// <summary>
/// Catches anything that escapes a service, logs it, and returns a generalized error so
/// internals are never exposed to the client.
/// </summary>
public class ErrorMiddleware(ILogger<ErrorMiddleware> logger, RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (UnauthorizedAccessException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(InternalError.Unauthorized("unauthorized").ToString());
        }
        catch (LyricBuilderException lyricBuilderException)
        {
            var error = new InternalError
            {
                InternalErrorCode = lyricBuilderException.InternalErrorCode,
                Message = lyricBuilderException.Message,
                FriendlyMessage = lyricBuilderException.FriendlyMessage.IsNullOrEmpty()
                    ? "an unexpected error occurred, please try it again"
                    : lyricBuilderException.FriendlyMessage
            };

            logger.LogError(lyricBuilderException, "handled error in system");
            context.Response.StatusCode = error.GetHttpStatusCode();
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(error.ToString());
        }
        catch (Exception e)
        {
            var error = new InternalError
            {
                InternalErrorCode = InternalErrorCode.InternalServerError,
                FriendlyMessage = "an unexpected error occurred, please try it again"
            };

            logger.LogError(e, "unhandled error in system");
            context.Response.StatusCode = error.GetHttpStatusCode();
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(error.ToString());
        }
    }
}

public static class ApiErrorHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseErrorMiddleware(this IApplicationBuilder builder) =>
        builder.UseMiddleware<ErrorMiddleware>();
}
