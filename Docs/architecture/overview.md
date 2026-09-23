# Architecture overview

Five projects. Dependencies point inward, toward code that knows nothing about the outside
world.

```
Host ──► Core ──► Infrastructure ──► Domain ──► Abstractions
  │        │            │              │
  └────────┴────────────┴──────────────┴──────► Abstractions
```

`Abstractions` sits at the centre: everything may reference it, it references nothing.

## The layers

### Abstractions

Contracts and shared vocabulary. `StatefulResult<T>`, `InternalError`, `IRepository<T>`,
`IUnitOfWork`, `WithPagination`.

It has one NuGet dependency (`DependencyInjection.Abstractions`, for the registration
helper) and no project references at all. That constraint is what makes it safe for every
other layer to depend on.

**`IRepository<T>` lives here, not in Infrastructure.** That is the inversion that makes the
whole thing work: `Core` calls the repository without referencing EF Core, so persistence can
be swapped without touching a service.

### Domain

Entities and enums: `Lyric` and its `LyricSection`s, `Style`, `Tag`. See
[Domain model](domain-model.md) for how they fit together.

Mostly plain data. Where a real rule exists it lives on the entity, not in a service: section
order is kept contiguous by `Lyric`'s own methods, which is why `Position` has no public
setter. New rules of that kind belong there too.

### Infrastructure

The EF Core implementation: `LyricBuilderDbContext`, `Repository<T>`, and the
`IEntityTypeConfiguration` classes.

This is the only layer that knows PostgreSQL exists. See [Persistence](persistence.md).

### Core

The application layer, and the composition root. Services, their models, the request-scoped
context, and the middlewares.

`AddCore` is the single entry point into everything: the Host calls it and nothing else.

### Host

ASP.NET Core. Controllers, the pipeline, configuration, logging.

Controllers hold no logic. They call a service and translate the result.

## Why Core references Infrastructure

This is the one place the layout departs from textbook Clean Architecture, and it is worth
being explicit about.

Strictly, `Core` should not reference `Infrastructure` — the Host should wire the
implementation to the interface. Here, `Core` references it so that `AddCore` can call
`AddLyricBuilderDatabase`, giving the Host exactly one line to call.

**What this costs:** `Core` compiles against the EF Core assembly, so it is possible to use
`DbContext` directly inside a service. Nothing but discipline prevents it.

**What it buys:** one composition entry point instead of a Host that has to know the internal
wiring of every layer.

If that trade stops being worth it, the fix is to move `AddLyricBuilderDatabase` into the
Host's `Program.cs` and drop the project reference. Nothing else has to change — services
already depend only on `IRepository<T>`.

## Where things live

| Kind of thing | Location |
|---|---|
| Result and error types | `Abstractions/StatefulResult.cs`, `Abstractions/Domain/` |
| Persistence contracts | `Abstractions/Persistence/` |
| Entities | `Domain/Entities/` |
| DbContext, repository | `Infrastructure/Persistence/` |
| Per-entity table mapping | `Infrastructure/Persistence/Configurations/` |
| Services | `Core/Services/<Feature>/` |
| Request/response models | `Core/Models/<Feature>/` |
| Cross-cutting request state | `Core/RequestContext.cs` |
| Middlewares | `Core/Middlewares/` |
| Controllers | `Host/Controllers/` (flat) |

Feature folders for models and services, flat for controllers — each business area has several
models and services, but exactly one controller, so a per-feature controller folder would
always hold a single file.

## Further reading

- [Domain model](domain-model.md) — lyrics, sections, styles, and what generation reads
- [Result pattern](result-pattern.md) — how failures travel
- [Models and DTOs](models-and-dtos.md) — why entities stop at the service boundary
- [Persistence](persistence.md) — repository, auditing, the UTC rule
- [Dependency injection](dependency-injection.md) — convention-based registration
