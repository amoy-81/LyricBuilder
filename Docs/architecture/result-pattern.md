# The result pattern

Expected failures are return values. A lyric that does not exist is a normal outcome, not an
exceptional one, so it does not throw.

## The three types

All in `Abstractions/StatefulResult.cs`, all implementing `IStatefulResult`:

| Type | Returned by | Carries |
|---|---|---|
| `StatefulResult<T>` | single-item reads | `Data` |
| `StatefulPagedResult<T>` | list reads | `Data` + `TotalCount` |
| `MutationOperationResult` | writes | `Id` of the affected entity + `Message` |

`IStatefulResult` exposes only `IsSuccess` and `Error`. That is what lets `CreateResponse`
translate any result without knowing its payload type.

`MutationOperationResult` returns the id so a caller does not need a follow-up read after
creating something.

## InternalError

A failure carries an `InternalError` with three parts:

```csharp
InternalError.NotFound("lyric 3f2a… not found for tenant 9c1b…")
```

| Field | Audience |
|---|---|
| `InternalErrorCode` | The system — maps to an HTTP status code |
| `Message` | The log. Technical detail. **Not guaranteed client-safe.** |
| `FriendlyMessage` | The client |

The split matters. `Message` can name an internal id or a table; `FriendlyMessage` is what
ends up in the response body. When a factory method is called with one argument, both are set
to the same string — so **if the technical message should not be public, pass both**:

```csharp
InternalError.Forbidden(
    "caller does not own this lyric",   // log
    "you cannot edit this lyric");      // client
```

`InternalErrorCode` is transport-agnostic on purpose. A service never mentions HTTP;
`GetHttpStatusCode()` does the mapping at the edge.

## The service shape

Every service method follows the same skeleton:

```csharp
public async Task<StatefulResult<LyricModel>> GetLyricByIdAsync(Guid id, CancellationToken ct)
{
    try
    {
        var lyric = await lyricRepository.GetByIdAsync(id, ct);
        if (lyric is null)
            return StatefulResult<LyricModel>.Failed(InternalError.NotFound("lyric not found"));

        return StatefulResult<LyricModel>.Success(LyricModel.Map(lyric));
    }
    catch (Exception e)
    {
        logger.LogError(e, "error while getting lyric {LyricId}", id);
        return StatefulResult<LyricModel>.Failed(
            InternalError.InternalServerError("error while getting lyric"));
    }
}
```

Three rules hold across every method:

1. **Expected failures return `Failed`** with a specific code.
2. **Unexpected exceptions are caught, logged with the exception object, and flattened** into
   a generic `InternalServerError`. The caught detail goes to the log, never to the client.
3. **The catch-all message is deliberately vague.** Internals must not leak through it.

## Reaching the client

`BaseEndpoints.CreateResponse` does the translation:

```csharp
protected IActionResult CreateResponse(IStatefulResult result) =>
    result.IsSuccess ? Ok(result) : CreateErrorResponse(result);
```

| `InternalErrorCode` | HTTP |
|---|---|
| `NotFound` | 404 |
| `BadRequest` | 400 |
| `Unauthorized` | 401 |
| `Forbidden` | 403 |
| `Conflict` | 409 |
| `ToManyRequest` | 429 |
| anything else | 500 |

This is why controllers have no branching. A controller that inspects `IsSuccess` itself is a
sign something belongs in the service.

## When to throw instead

Exceptions are still right for failures that should unwind the stack — a broken invariant, a
guard clause, a bug. `LyricBuilderException` carries the same `InternalErrorCode`, and
`ErrorMiddleware` converts it into the same response shape.

The dividing line: **if a caller can reasonably handle it, return it. If it means the program
is wrong, throw it.**

`ErrorMiddleware` is a backstop, not a strategy. A service that lets exceptions escape as a
matter of course loses the logging context its own catch block would have provided.

## Trade-offs

Honest about the cost:

- **The try/catch is repeated** in every method. That repetition is accepted deliberately:
  services stay independently changeable, with no shared CRUD base class to work around.
- **Results can be ignored.** Nothing forces a caller to check `IsSuccess`; unlike an
  exception, a dropped failure is silent.
- **No chaining.** Libraries like `Ardalis.Result` or `FluentResults` offer `Map`/`Bind`
  composition. This is deliberately plainer, and adds no dependency.
