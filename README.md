# LyricBuilder

A lyric authoring platform for music, built on .NET 10 with a Clean Architecture layout.

> **Status: early scaffolding.** The architecture, persistence, and one sample feature
> (`Lyric`) are in place. Authentication is not wired up yet — see
> [Known gaps](#known-gaps) before building on this.

## Table of contents

- [Requirements](#requirements)
- [Getting started](#getting-started)
- [Solution layout](#solution-layout)
- [How a request flows](#how-a-request-flows)
- [Key conventions](#key-conventions)
- [Adding a feature](#adding-a-feature)
- [Database](#database)
- [Configuration](#configuration)
- [Known gaps](#known-gaps)

## Requirements

| | |
|---|---|
| .NET SDK | 10.0 |
| PostgreSQL | 14+ (developed against 17) |

## Getting started

```bash
# 1. Point the app at your PostgreSQL instance.
#    user-secrets keeps the password out of source control.
dotnet user-secrets set "ConnectionStrings:LyricBuilderDatabase" \
  "Host=localhost;Port=5432;Database=lyricbuilder;Username=postgres;Password=<your-password>" \
  --project Src/LyricBuilder.Host

# 2. Create the database schema.
dotnet ef database update -p Src/LyricBuilder.Infrastructure -s Src/LyricBuilder.Host

# 3. Run.
dotnet run --project Src/LyricBuilder.Host
```

Then open **`/scalar/v1`** for interactive API docs, or `/openapi/v1.json` for the raw
document. `/live` is the health check.

> No migration exists yet — run `dotnet ef migrations add Initial -p Src/LyricBuilder.Infrastructure -s Src/LyricBuilder.Host`
> once before step 2.

## Solution layout

```
LyricBuilder.sln
Directory.Build.props        Shared build settings (net10.0, nullable, implicit usings)
Directory.Packages.props     Central package versions — csproj files carry no version numbers
Src/
  LyricBuilder.Abstractions  Result types, error model, repository contracts. Depends on nothing.
  LyricBuilder.Domain        Entities and enums.
  LyricBuilder.Infrastructure EF Core: DbContext, repository, entity configuration.
  LyricBuilder.Core          Application layer: services and their models. The composition root.
  LyricBuilder.Host          ASP.NET Core API: controllers, pipeline, configuration.
```

Dependencies point inward:

```
Host ──► Core ──► Infrastructure ──► Domain ──► Abstractions
                     └──────────────────┴──────────►
```

`Abstractions` is referenced by every layer and references none, so contracts can be shared
without coupling.

## How a request flows

A read, end to end:

```
HTTP GET /api/lyrics
  └─ LyricsController            binds LyricFilter from the query string
      └─ ILyricService           builds the query, maps entities → LyricModel
          └─ IRepository<Lyric>  EF Core, no-tracking
      ◄─ StatefulPagedResult<LyricModel>
  ◄─ CreateResponse(...)         result → HTTP status code
```

The controller contains no branching logic. It calls a service and hands the result to
`CreateResponse`, which maps the failure code to a status code.

## Key conventions

These are the load-bearing ideas. Understanding them is most of what you need.

### Failure is a value, not an exception

Services return `StatefulResult<T>`, `StatefulPagedResult<T>`, or `MutationOperationResult`.
Each carries `IsSuccess` plus an `InternalError` describing what went wrong.

`InternalError` separates two audiences: `Message` is technical detail for the log, and
`FriendlyMessage` is what the client is allowed to see.

`ErrorMiddleware` is the backstop for anything that escapes a service.

### Entities never cross the API boundary

Every feature has three model types, in `Core/Models/<Feature>/`:

| Type | Direction | Purpose |
|---|---|---|
| `XModel` | out | Response shape. Has a static `Map` from the entity. |
| `XMutation` | in | Create/update body. |
| `XFilter` | in | Query-string filter; extends `WithPagination`. |

`XMutation` deliberately omits server-owned fields. `LyricMutation` has no `AuthorId` or
`Kind`, so a client cannot claim authorship or pass its lyric off as a reference example — the
service sets both.

### Services register themselves by name

`AddCore` calls `RegisterByNamingConvention(assembly, "Service")`, which registers every class
ending in `Service` against the interfaces it implements. A new service needs **no** DI line.

A type named outside that convention — a `…Guard`, a `…Notifier` — must be registered
explicitly in `AddCore`.

### Auditing and soft-delete are automatic

`BaseEntity` carries `Id` (a sequential `Guid` v7), `CreatedAt`, `UpdatedAt`, `IsDeleted`, and
`DeletedAt`.

`LyricBuilderDbContext.ApplyAuditInformation` stamps the timestamps on save, so no write path
can forget them, and `CreatedAt` is protected from being overwritten on update.

`Repository.Remove` soft-deletes any `BaseEntity` rather than issuing a `DELETE`. A global
query filter hides those rows; use `IgnoreQueryFilters()` to see them.

### Writes are staged, not immediate

`IRepository` methods stage changes. Nothing reaches the database until
`IUnitOfWork.SaveChangesAsync`, so a service can compose several writes into one transaction.

## Adding a feature

Adding `Album` means three parallel additions, and no DI wiring:

```
Src/LyricBuilder.Domain/Entities/Album.cs
Src/LyricBuilder.Core/Models/Albums/AlbumModel.cs, AlbumMutation.cs, AlbumFilter.cs
Src/LyricBuilder.Core/Services/Albums/AlbumService.cs        ← picked up automatically
Src/LyricBuilder.Infrastructure/Persistence/Configurations/AlbumConfiguration.cs
Src/LyricBuilder.Host/Controllers/AlbumsController.cs
```

`Models` and `Services` are grouped into per-feature folders; `Controllers` is flat, because
each business area has exactly one controller.

Shared pieces stay at the root of their folder — `BaseEndpoint.cs`, `RequestContext.cs`,
the middlewares.

Services are deliberately written out in full rather than sharing a CRUD base class. The
repetition is the cost of keeping each feature independently changeable.

## Database

PostgreSQL via Npgsql. Entity and property names map to table and column names as written
(`PascalCase`).

**Every `DateTime` is forced to UTC** by a global value converter in `LyricBuilderDbContext`.
This is not optional on Npgsql: it maps `DateTime` to `timestamp with time zone` and throws on
any value whose `Kind` is not `Utc`, and values read back from the database arrive as
`Unspecified`. The converter handles both directions centrally.

> Because `DateTime` does not round-trip its `Kind` through the database, treat every timestamp
> as UTC and convert at the edge. Do not call `ToLocalTime()` on a value straight from a query.

Migrations:

```bash
dotnet ef migrations add <Name> -p Src/LyricBuilder.Infrastructure -s Src/LyricBuilder.Host
dotnet ef database update      -p Src/LyricBuilder.Infrastructure -s Src/LyricBuilder.Host
```

## Configuration

| Key | Purpose |
|---|---|
| `ConnectionStrings:LyricBuilderDatabase` | PostgreSQL connection. Required — startup throws without it. |
| `Serilog` | Logging, split across `Serilog.json` and `Serilog.<Environment>.json`. |

`appsettings.Development.json` ships with a placeholder password. Prefer `user-secrets` for
real credentials — that file is not gitignored.

## Known gaps

Deliberately unfinished, in rough priority order:

1. **Authentication.** The pipeline calls `UseAuthentication`, but no scheme is registered.
   `[Authorize]` on `SecureEndpoint` will not work until a JWT scheme is added, and
   `RequestContext.UserId` is always `null` — so **create and update currently fail with
   `Unauthorized`**. `LyricsController` extends `PublicEndpoint` so reads remain testable
   in the meantime.
2. **No migration.** The schema has never been generated.
3. **No tests.** No test project exists yet.
4. **Validation** lives in `LyricMutation` as data annotations. Once rules grow beyond simple
   shape checks, move them into the service or adopt FluentValidation.
