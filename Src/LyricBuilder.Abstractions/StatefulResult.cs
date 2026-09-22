using LyricBuilder.Abstractions.Domain;

namespace LyricBuilder.Abstractions;

/// <summary>
/// Non-generic view of a result, so the API layer can translate any result to a response
/// without knowing its payload type.
/// </summary>
public interface IStatefulResult
{
    bool IsSuccess { get; }
    InternalError? Error { get; }
}

/// <summary>
/// The return type of every read operation in a service. Failure is a value, not an exception —
/// services catch, log, and return <see cref="Failed"/>.
/// </summary>
public class StatefulResult<T> : IStatefulResult
{
    public bool IsSuccess { get; private init; }
    public T? Data { get; private init; }
    public InternalError? Error { get; private init; }

    public static StatefulResult<T> Success(T data) => new() { IsSuccess = true, Data = data };

    public static StatefulResult<T> Failed(InternalError error) => new() { IsSuccess = false, Error = error };
}

/// <summary>
/// A page of results plus the unpaged total, for list endpoints.
/// </summary>
public class StatefulPagedResult<T> : IStatefulResult
{
    public bool IsSuccess { get; private init; }
    public IReadOnlyCollection<T> Data { get; private init; } = [];
    public long TotalCount { get; private init; }
    public InternalError? Error { get; private init; }

    public static StatefulPagedResult<T> Success(IReadOnlyCollection<T> data, long totalCount) =>
        new() { IsSuccess = true, Data = data, TotalCount = totalCount };

    public static StatefulPagedResult<T> Failed(InternalError error) =>
        new() { IsSuccess = false, Error = error };
}

/// <summary>
/// The return type of every write operation. Carries the affected entity's id so the caller
/// does not need a follow-up read.
/// </summary>
public class MutationOperationResult : IStatefulResult
{
    public bool IsSuccess { get; private init; }
    public Guid? Id { get; private init; }
    public string Message { get; private init; } = string.Empty;
    public InternalError? Error { get; private init; }

    public static MutationOperationResult Success(Guid? id = null, string message = "") =>
        new() { IsSuccess = true, Id = id, Message = message };

    public static MutationOperationResult Failed(InternalError error) =>
        new() { IsSuccess = false, Error = error, Message = error.FriendlyMessage };
}
