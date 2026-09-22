using LyricBuilder.Abstractions;
using LyricBuilder.Abstractions.Domain;
using LyricBuilder.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LyricBuilder.Host.Controllers;

/// <summary>
/// Translates a service result into an HTTP response. Controllers carry no branching logic of
/// their own — they call a service and hand the result to CreateResponse.
/// </summary>
[ApiController]
[ApiConventionType(typeof(DefaultApiConventions))]
[Produces("application/json")]
public class BaseEndpoints(ILogger logger, RequestContext requestContext) : ControllerBase
{
    protected readonly ILogger Logger = logger;

    protected readonly RequestContext RequestContext = requestContext;

    protected CancellationToken RequestCancellationToken => HttpContext.RequestAborted;

    protected IActionResult CreateResponse(MutationOperationResult result) =>
        result.IsSuccess ? Ok(result) : CreateErrorResponse(result);

    protected IActionResult CreateResponse(IStatefulResult result) =>
        result.IsSuccess ? Ok(result) : CreateErrorResponse(result);

    private IActionResult CreateErrorResponse(IStatefulResult result)
    {
        if (result.Error is null)
            return StatusCode(500, "Unexpected error, please contact support team");

        Logger.LogError("request failed: {ErrorMessage}", result.Error.Message);

        return result.Error.InternalErrorCode switch
        {
            InternalErrorCode.NotFound => NotFound(result),
            InternalErrorCode.BadRequest => BadRequest(result),
            InternalErrorCode.Unauthorized => Unauthorized(result),
            InternalErrorCode.Forbidden => StatusCode(403, result),
            InternalErrorCode.Conflict => Conflict(result),
            InternalErrorCode.ToManyRequest => StatusCode(429, result),
            _ => StatusCode(500, result)
        };
    }
}

/// <summary>Endpoints that require an authenticated caller.</summary>
[Authorize]
public class SecureEndpoint(ILogger logger, RequestContext requestContext)
    : BaseEndpoints(logger, requestContext);

/// <summary>Endpoints open to anonymous callers.</summary>
public class PublicEndpoint(ILogger logger, RequestContext requestContext)
    : BaseEndpoints(logger, requestContext);
