namespace LyricBuilder.Abstractions.Domain.Exceptions;

/// <summary>
/// Thrown when a failure must unwind the call stack rather than flow back as a result
/// (guard clauses, invariant violations). The error middleware turns it into an
/// <see cref="InternalError"/> response.
/// </summary>
public class LyricBuilderException : Exception
{
    public InternalErrorCode InternalErrorCode { get; }

    public string FriendlyMessage { get; }

    public LyricBuilderException(
        string message,
        InternalErrorCode internalErrorCode = InternalErrorCode.InternalServerError,
        string? friendlyMessage = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        InternalErrorCode = internalErrorCode;
        FriendlyMessage = string.IsNullOrWhiteSpace(friendlyMessage) ? message : friendlyMessage;
    }

    public static LyricBuilderException NotFound(string message, string? friendlyMessage = null) =>
        new(message, InternalErrorCode.NotFound, friendlyMessage);

    public static LyricBuilderException BadRequest(string message, string? friendlyMessage = null) =>
        new(message, InternalErrorCode.BadRequest, friendlyMessage);

    public static LyricBuilderException Forbidden(string message, string? friendlyMessage = null) =>
        new(message, InternalErrorCode.Forbidden, friendlyMessage);
}
