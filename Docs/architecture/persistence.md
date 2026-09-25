# Persistence

EF Core over PostgreSQL, behind `IRepository<T>` and `IUnitOfWork`.

Both contracts live in `Abstractions/Persistence/`, not in Infrastructure. That is what lets a
service query without referencing EF Core.

## Repository and unit of work

`Repository<T>` is registered open-generic, so every entity gets one with no per-type class:

```csharp
services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<LyricBuilderDbContext>());
```

`LyricBuilderDbContext` implements `IUnitOfWork` itself, and the registration resolves the
same scoped instance — so the repository and the unit of work always share one change tracker.

### Writes are staged

`AddAsync`, `Update`, and `Remove` stage changes. Nothing reaches the database until:

```csharp
await unitOfWork.SaveChangesAsync(ct);
```

This is what lets a service compose several writes into one transaction. It also means
**forgetting the save silently does nothing** — no error, no persistence. If a write appears
to vanish, check for the missing `SaveChangesAsync` first.

### Reads are untracked

`Query()`, `ListAsync`, and `ListPagedAsync` use `AsNoTracking()` — faster, and they never
accidentally persist a mutation.

`GetByIdAsync` and `FirstOrDefaultAsync` **do** track, because they are the entry point for
updates: load, mutate, save.

`QueryTracked()` is the tracked counterpart of `Query()`, for when an update needs an entity
**together with its children**. Changing a lyric's sections is the case it exists for:
`Lyric`'s section methods work on the loaded collection, so the lyric has to be loaded with
`Include(l => l.Sections)` and tracked, then saved without calling `Update`:

```csharp
var lyric = await lyricRepository.QueryTracked()
    .Include(l => l.Sections)
    .FirstOrDefaultAsync(l => l.Id == lyricId, ct);

lyric.AddSection(SectionType.Chorus);
await unitOfWork.SaveChangesAsync(ct);   // the change tracker finds the new section
```

`Query()` returns `IQueryable<T>` for reads a method signature cannot express. It composes
server-side, so filtering and paging still happen in the database. The cost is that EF Core
leaks into the service — acceptable for queries, but a service building a `Query()` chain
dozens of lines long is asking for a dedicated repository method.

## Auditing

`BaseEntity` carries:

| Property | Set by |
|---|---|
| `Id` | `Guid.CreateVersion7()` at construction — never by EF Core or the database |
| `CreatedAt` | `SaveChanges`, on insert |
| `UpdatedAt` | `SaveChanges`, on update |
| `IsDeleted`, `DeletedAt` | `Repository.Remove` |

`ApplyAuditInformation` runs inside both `SaveChanges` overloads, so no write path can skip
it. On update it also does this:

```csharp
entry.Property(e => e.CreatedAt).IsModified = false;
```

Without that line, saving a detached entity whose `CreatedAt` was never loaded would overwrite
the real creation time with a default.

### Ids are client-assigned, and EF Core is told so

`LyricBuilderDbContext` marks every `Id` as `ValueGeneratedNever`. Without that, EF Core's
convention treats a Guid key as generated on insert, and so reads any entity whose key is
already set as one that exists in the database. Every entity here has its key set from
construction, so a new section added to a tracked lyric was saved as an `UPDATE` that matched
no row, and failed with `DbUpdateConcurrencyException`.

`Repository.Update` still marks everything it is given as modified, new or not. Do not call it
on a tracked entity whose children were added; just save.

### Why Guid v7

Version 7 GUIDs are time-ordered. Random v4 GUIDs scatter inserts across a B-tree index and
fragment it; v7 values append, which keeps index maintenance cheap. They also sort by creation
time, which is occasionally useful on its own.

## Soft delete

`Repository.Remove` does not issue a `DELETE` for anything deriving from `BaseEntity`:

```csharp
auditable.IsDeleted = true;
auditable.DeletedAt = DateTime.UtcNow;
```

A global query filter in `LyricConfiguration` hides those rows:

```csharp
builder.HasQueryFilter(l => !l.IsDeleted);
```

Two consequences worth knowing:

- **The filter is per-entity.** It is declared in each entity's configuration. A new entity
  without that line will return soft-deleted rows.
- **Unique indexes still see deleted rows.** A deleted lyric keeps occupying its slug or title
  as far as a unique constraint is concerned. Use a filtered index when that matters.

`IgnoreQueryFilters()` bypasses the filter for admin or audit queries.

## PostgreSQL and the UTC rule

**Every `DateTime` is coerced to UTC** by a global value converter in
`LyricBuilderDbContext.ApplyUtcDateTimeConversion`.

This is not a style preference. Npgsql maps `DateTime` to `timestamp with time zone` and
**throws** on any value whose `Kind` is not `Utc`. Values read back from PostgreSQL arrive as
`Unspecified`, so without the converter an entity that was loaded and re-saved would crash on
the second save — a bug that only appears on update, never on insert.

The converter normalises both directions, for every `DateTime` and `DateTime?` in the model,
so no entity or caller has to remember.

### What this still does not fix

`DateTime` does not carry its `Kind` through the database. The converter restores it on read,
but the rule to work by is:

> **Treat every timestamp as UTC. Convert at the edge, for display only.**

Do not call `ToLocalTime()` on a value from a query and store the result. If time zones become
genuinely important, `DateTimeOffset` is the better type.

## Table and column naming

Names map as written — `PascalCase` tables and columns (`Lyrics`, `CreatedAt`, `AuthorId`).

This departs from the PostgreSQL convention of `snake_case`, and it was a deliberate choice:
it keeps C# and SQL identical, at the cost of needing double quotes in hand-written `psql`
queries.

```sql
SELECT "Title", "CreatedAt" FROM "Lyrics" WHERE NOT "IsDeleted";
```

## Entity configuration

Each entity gets an `IEntityTypeConfiguration` in
`Infrastructure/Persistence/Configurations/`. They are discovered automatically:

```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(LyricBuilderDbContext).Assembly);
```

Adding an entity means adding a configuration file — never editing `OnModelCreating`.

## Migrations

```bash
dotnet ef migrations add <Name> -p Src/LyricBuilder.Infrastructure -s Src/LyricBuilder.Host
dotnet ef database update      -p Src/LyricBuilder.Infrastructure -s Src/LyricBuilder.Host
```

`-p` is where migrations are written; `-s` is the project holding the connection string.

**No migration exists yet.** The schema has never been generated — see
[local setup](../guides/local-setup.md).
