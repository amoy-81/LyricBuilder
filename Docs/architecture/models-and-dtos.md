# Models and DTOs

`Core/Models/` holds what other codebases call DTOs. The folder is named `Models`; the
contents are the same thing.

Entities stop at the service boundary. Nothing in `Domain/Entities/` is ever accepted from or
returned to a client.

## Three types per feature

In `Core/Models/<Feature>/`, one file each:

| Type | Direction | Role |
|---|---|---|
| `XModel` | response | What a client is allowed to see. Has a static `Map` from the entity. |
| `XMutation` | request | Create/update body. |
| `XFilter` | request | Query-string filter. Extends `WithPagination`. |

## Why not return the entity

Three reasons, all visible in the current code.

### 1. It would leak persistence state

`Lyric` inherits `IsDeleted` and `DeletedAt` from `BaseEntity`. Serialize the entity and both
appear in the response — internal bookkeeping a client has no business seeing.

### 2. It would let clients write server-owned fields

This is the important one. `LyricMutation` deliberately has **no `AuthorId` and no `Kind`**.

If the entity were the request body, a client could post:

```json
{ "title": "…", "styleId": "…", "authorId": "<someone-else>", "kind": 1 }
```

…and forge authorship, or turn its own lyric into a reference example that generation
learns from. The attack is called **over-posting** (or mass
assignment), and the defence is that those fields do not exist on the type the model binder
fills. The service sets them:

```csharp
AuthorId = requestContext.UserId.Value,   // from the token, never the body
Kind = LyricKind.Original                 // the server decides
```

**When adding a mutation type, start from what the client may set — not by copying the
entity and deleting fields.** The second approach fails open: a new entity property silently
becomes writable.

### 3. It couples the API contract to the schema

Rename a column or split one into its own table, and `LyricModel` absorbs the change inside
its `Map`. No client breaks.

## Mapping

`Map` is a static method on the model, next to the fields it fills:

```csharp
public static LyricModel Map(Lyric lyric, bool includeContent = true) => new() { … };
```

No AutoMapper. Hand-written mapping is a few obvious lines, fails at compile time when a
property changes, and is trivial to step through. Convention-based mapping trades that for
runtime surprises.

### The `includeContent` flag

Lyric bodies are large. Returning twenty of them in a list page wastes bandwidth nobody asked
for, so lists pass `includeContent: false` and single reads pass `true`.

`LyricModel.Content` is therefore `string?` — **null in a list response**. The entity has no
`Content` column at all: the text lives in its sections, and `Map` composes it with
`Lyric.ComposeText()`. So a single read must load the sections (`Include(l => l.Sections)`), or
it returns an empty string rather than failing.

## Filters and paging

`XFilter` extends `WithPagination`, which self-clamps:

- `Page` below 1 becomes 1
- `PageSize` is clamped to `1..100` (`WithPagination.MaxPageSize`)
- `Skip` is computed, not supplied

Clamping lives in the property setters, so a caller cannot request an unbounded page no matter
what it sends.

## Validation

`LyricMutation` carries data annotations (`[Required]`, `[MaxLength]`, `[Range]`). ASP.NET
Core enforces them before the action runs, thanks to `[ApiController]`.

These are **shape** checks — length, presence, numeric range. They live on the model because
they describe the contract.

Business rules do not belong here. "A repeat must point at a section of the same lyric" depends
on state the model cannot see; that belongs in the service or on the entity.

Note the layering wrinkle: `DataAnnotations` is an API concern sitting in `Core`. It is
tolerable while rules stay simple. Once they need context, move to FluentValidation or push
the rule into the service.

## One file per type

Each of the three types is its own file. They share a folder, not a file.

An earlier revision put all three in one `LyricModels.cs`. Grouping reads well when the types
are tiny, but per-file is easier to navigate as they grow, and matches the rest of the
codebase.

## Naming, across schools

The same idea under different names, if you are coming from elsewhere:

| School | Response | Request |
|---|---|---|
| **This project** | `XModel` | `XMutation` |
| Classic DTO | `XDto` | `CreateXDto` / `UpdateXDto` |
| CQRS / MediatR | `XResponse`, `XVm` | `CreateXCommand`, `GetXQuery` |
| REST design | `XResource` | `XRequest` |
| DDD | `XReadModel` | `XCommand` |

"Mutation" is borrowed from GraphQL, where queries read and mutations write. The term carries
over cleanly to REST.
