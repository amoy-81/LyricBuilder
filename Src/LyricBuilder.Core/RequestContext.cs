namespace LyricBuilder.Core;

/// <summary>
/// Per-request ambient state (who is calling). Populated once by
/// <see cref="Middlewares.RequestContextMiddleware"/> and injected into services, so no service
/// needs to touch <c>HttpContext</c>.
/// </summary>
/// <remarks>Scoped — one instance per request.</remarks>
public class RequestContext
{
    public Guid? UserId { get; set; }

    public string? UserName { get; set; }

    public bool IsAuthenticated => UserId is not null;
}
