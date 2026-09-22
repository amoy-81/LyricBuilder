namespace LyricBuilder.Abstractions.Domain;

/// <summary>
/// Transport-agnostic failure classification. Mapped to an HTTP status code at the edge
/// (see <see cref="InternalError.GetHttpStatusCode"/>) so services never reference HTTP.
/// </summary>
public enum InternalErrorCode
{
    None = 0,
    BadRequest = 400,
    Unauthorized = 401,
    Forbidden = 403,
    NotFound = 404,
    Conflict = 409,
    ToManyRequest = 429,
    InternalServerError = 500
}
