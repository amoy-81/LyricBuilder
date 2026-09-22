using System.Text.Json;

namespace LyricBuilder.Abstractions.Domain;

/// <summary>
/// A failure carried inside a result instead of thrown. <see cref="Message"/> is for logs,
/// <see cref="FriendlyMessage"/> is what the client is allowed to see.
/// </summary>
public class InternalError
{
    public InternalErrorCode InternalErrorCode { get; init; } = InternalErrorCode.InternalServerError;

    /// <summary>Technical detail — logged, never guaranteed to be client-safe.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Client-facing text.</summary>
    public string FriendlyMessage { get; init; } = string.Empty;

    public static InternalError BadRequest(string message, string? friendlyMessage = null) =>
        Create(InternalErrorCode.BadRequest, message, friendlyMessage);

    public static InternalError Unauthorized(string message, string? friendlyMessage = null) =>
        Create(InternalErrorCode.Unauthorized, message, friendlyMessage);

    public static InternalError Forbidden(string message, string? friendlyMessage = null) =>
        Create(InternalErrorCode.Forbidden, message, friendlyMessage);

    public static InternalError NotFound(string message, string? friendlyMessage = null) =>
        Create(InternalErrorCode.NotFound, message, friendlyMessage);

    public static InternalError Conflict(string message, string? friendlyMessage = null) =>
        Create(InternalErrorCode.Conflict, message, friendlyMessage);

    public static InternalError ToManyRequest(string message, string? friendlyMessage = null) =>
        Create(InternalErrorCode.ToManyRequest, message, friendlyMessage);

    public static InternalError InternalServerError(string message, string? friendlyMessage = null) =>
        Create(InternalErrorCode.InternalServerError, message, friendlyMessage);

    private static InternalError Create(InternalErrorCode code, string message, string? friendlyMessage) => new()
    {
        InternalErrorCode = code,
        Message = message,
        FriendlyMessage = string.IsNullOrWhiteSpace(friendlyMessage) ? message : friendlyMessage
    };

    public int GetHttpStatusCode() => InternalErrorCode switch
    {
        InternalErrorCode.BadRequest => 400,
        InternalErrorCode.Unauthorized => 401,
        InternalErrorCode.Forbidden => 403,
        InternalErrorCode.NotFound => 404,
        InternalErrorCode.Conflict => 409,
        InternalErrorCode.ToManyRequest => 429,
        _ => 500
    };

    public override string ToString() =>
        JsonSerializer.Serialize(new { code = InternalErrorCode.ToString(), message = FriendlyMessage });
}
