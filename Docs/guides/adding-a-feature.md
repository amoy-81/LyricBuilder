# Adding a feature

Walking through `Album`, from entity to endpoint. Six files, no DI wiring.

```
Src/LyricBuilder.Domain/Entities/Album.cs
Src/LyricBuilder.Core/Models/Albums/AlbumModel.cs
Src/LyricBuilder.Core/Models/Albums/AlbumMutation.cs
Src/LyricBuilder.Core/Models/Albums/AlbumFilter.cs
Src/LyricBuilder.Core/Services/Albums/AlbumService.cs
Src/LyricBuilder.Infrastructure/Persistence/Configurations/AlbumConfiguration.cs
Src/LyricBuilder.Host/Controllers/AlbumsController.cs
```

Models and services sit in per-feature folders. Controllers stay flat — one per business area.

## 1. The entity

```csharp
// Domain/Entities/Album.cs
namespace LyricBuilder.Domain.Entities;

public class Album : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public Guid ArtistId { get; set; }
    public DateOnly? ReleasedOn { get; set; }
}
```

Inheriting `BaseEntity` brings `Id`, `CreatedAt`, `UpdatedAt`, and soft-delete — all handled
by the DbContext.

## 2. The models

Three files. See [models and DTOs](../architecture/models-and-dtos.md) for why three.

```csharp
// Core/Models/Albums/AlbumModel.cs
namespace LyricBuilder.Core.Models.Albums;

public class AlbumModel
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public Guid ArtistId { get; init; }
    public DateOnly? ReleasedOn { get; init; }
    public DateTime CreatedAt { get; init; }

    public static AlbumModel Map(Album album) => new()
    {
        Id = album.Id,
        Title = album.Title,
        ArtistId = album.ArtistId,
        ReleasedOn = album.ReleasedOn,
        CreatedAt = album.CreatedAt
    };
}
```

```csharp
// Core/Models/Albums/AlbumMutation.cs
using System.ComponentModel.DataAnnotations;

namespace LyricBuilder.Core.Models.Albums;

public class AlbumMutation
{
    [Required]
    [MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    public DateOnly? ReleasedOn { get; init; }
}
```

> **Write the mutation from what a client may set** — do not copy the entity and delete
> fields. `ArtistId` is absent because the server assigns it. Starting from the entity fails
> open: the next property you add becomes writable by accident.

```csharp
// Core/Models/Albums/AlbumFilter.cs
namespace LyricBuilder.Core.Models.Albums;

public class AlbumFilter : WithPagination
{
    public Guid? ArtistId { get; set; }
    public string? Search { get; set; }
}
```

`WithPagination` clamps `Page` and `PageSize` itself.

## 3. The service

Interface and implementation in one file, matching `LyricService`.

```csharp
// Core/Services/Albums/AlbumService.cs
using LyricBuilder.Core.Models.Albums;
using Microsoft.EntityFrameworkCore;

namespace LyricBuilder.Core.Services.Albums;

public interface IAlbumService
{
    Task<StatefulPagedResult<AlbumModel>> GetAlbumsAsync(AlbumFilter filter, CancellationToken ct);
    Task<StatefulResult<AlbumModel>> GetAlbumByIdAsync(Guid id, CancellationToken ct);
    Task<MutationOperationResult> CreateAlbumAsync(AlbumMutation mutation, CancellationToken ct);
}

public sealed class AlbumService(
    ILogger<AlbumService> logger,
    IRepository<Album> albumRepository,
    IUnitOfWork unitOfWork,
    RequestContext requestContext) : IAlbumService
{
    public async Task<StatefulPagedResult<AlbumModel>> GetAlbumsAsync(
        AlbumFilter filter, CancellationToken ct)
    {
        try
        {
            var query = albumRepository.Query();

            if (filter.ArtistId is not null)
                query = query.Where(a => a.ArtistId == filter.ArtistId);

            if (filter.Search.IsNotNullOrEmpty())
                query = query.Where(a => a.Title.Contains(filter.Search));

            var total = await query.LongCountAsync(ct);
            var albums = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip(filter.Skip)
                .Take(filter.PageSize)
                .ToListAsync(ct);

            return StatefulPagedResult<AlbumModel>.Success(
                albums.Select(AlbumModel.Map).ToList(), total);
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while getting albums");
            return StatefulPagedResult<AlbumModel>.Failed(
                InternalError.InternalServerError("error while getting albums"));
        }
    }

    public async Task<StatefulResult<AlbumModel>> GetAlbumByIdAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var album = await albumRepository.GetByIdAsync(id, ct);
            if (album is null)
                return StatefulResult<AlbumModel>.Failed(InternalError.NotFound("album not found"));

            return StatefulResult<AlbumModel>.Success(AlbumModel.Map(album));
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while getting album {AlbumId}", id);
            return StatefulResult<AlbumModel>.Failed(
                InternalError.InternalServerError("error while getting album"));
        }
    }

    public async Task<MutationOperationResult> CreateAlbumAsync(
        AlbumMutation mutation, CancellationToken ct)
    {
        try
        {
            if (requestContext.UserId is null)
                return MutationOperationResult.Failed(
                    InternalError.Unauthorized("no authenticated user"));

            var album = new Album
            {
                Title = mutation.Title,
                ReleasedOn = mutation.ReleasedOn,
                ArtistId = requestContext.UserId.Value   // server-assigned, never from the body
            };

            await albumRepository.AddAsync(album, ct);
            await unitOfWork.SaveChangesAsync(ct);       // nothing persists without this

            return MutationOperationResult.Success(album.Id, "album created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while creating album");
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while creating album"));
        }
    }
}
```

Four things to carry over every time:

- **Name it `…Service`** — that is what gets it registered. See
  [dependency injection](../architecture/dependency-injection.md).
- **Catch, log with the exception, return a vague `InternalServerError`.** Detail goes to the
  log, not the client.
- **`SaveChangesAsync` or nothing persists.** Silently.
- **Server-owned fields come from `RequestContext`,** never the mutation.

## 4. The entity configuration

```csharp
// Infrastructure/Persistence/Configurations/AlbumConfiguration.cs
using LyricBuilder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LyricBuilder.Infrastructure.Persistence.Configurations;

public class AlbumConfiguration : IEntityTypeConfiguration<Album>
{
    public void Configure(EntityTypeBuilder<Album> builder)
    {
        builder.ToTable("Albums");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title).IsRequired().HasMaxLength(200);

        builder.HasIndex(a => a.ArtistId);

        // Required for soft-delete. Without it, deleted rows come back in every query.
        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
```

Discovered automatically by `ApplyConfigurationsFromAssembly` — no `OnModelCreating` edit.

**The query filter is per-entity.** Forgetting it is the most common mistake when adding an
entity: soft-deleted rows silently reappear.

Add the `DbSet` to `LyricBuilderDbContext`:

```csharp
public DbSet<Album> Albums => Set<Album>();
```

## 5. The controller

```csharp
// Host/Controllers/AlbumsController.cs
using LyricBuilder.Core;
using LyricBuilder.Core.Models.Albums;
using LyricBuilder.Core.Services.Albums;
using Microsoft.AspNetCore.Mvc;

namespace LyricBuilder.Host.Controllers;

[Route("api/albums")]
public class AlbumsController(
    ILogger<AlbumsController> logger,
    RequestContext requestContext,
    IAlbumService albumService) : PublicEndpoint(logger, requestContext)
{
    /// <summary>Albums matching the filter.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAlbums([FromQuery] AlbumFilter filter)
    {
        var result = await albumService.GetAlbumsAsync(filter, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>One album by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAlbumById([FromRoute] Guid id)
    {
        var result = await albumService.GetAlbumByIdAsync(id, RequestCancellationToken);
        return CreateResponse(result);
    }

    /// <summary>Creates an album owned by the caller.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAlbum([FromBody] AlbumMutation mutation)
    {
        var result = await albumService.CreateAlbumAsync(mutation, RequestCancellationToken);
        return CreateResponse(result);
    }
}
```

Every action is the same three lines: call the service, pass the result to `CreateResponse`,
return. **A controller that inspects `IsSuccess` itself means logic ended up in the wrong
layer.**

Extend `SecureEndpoint` instead of `PublicEndpoint` to require authentication — though until
a JWT scheme is registered, that will reject every request.

XML comments become the descriptions in `/scalar/v1`.

## 6. Migration

```bash
dotnet ef migrations add AddAlbum -p Src/LyricBuilder.Infrastructure -s Src/LyricBuilder.Host
dotnet ef database update         -p Src/LyricBuilder.Infrastructure -s Src/LyricBuilder.Host
```

## What you did not do

No DI registration. `AlbumService` ends in `Service`, so
`RegisterByNamingConvention` finds it. `IRepository<Album>` already resolves — the repository
is registered open-generic.

## Checklist

- [ ] Entity inherits `BaseEntity`
- [ ] `DbSet<T>` added to the DbContext
- [ ] Configuration file, **including `HasQueryFilter`**
- [ ] Mutation written from the client's perspective, omitting server-owned fields
- [ ] Service named `…Service`, every method wrapped in try/catch with logging
- [ ] `SaveChangesAsync` after every write
- [ ] Controller actions do nothing but call and translate
- [ ] Migration generated and applied
