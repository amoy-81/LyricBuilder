using LyricBuilder.Abstractions.Persistence;
using LyricBuilder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LyricBuilder.Infrastructure.Persistence;

public class LyricBuilderDbContext(DbContextOptions<LyricBuilderDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Lyric> Lyrics => Set<Lyric>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Picks up every IEntityTypeConfiguration in this assembly, so adding an entity means
        // adding a config file — never editing this method.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LyricBuilderDbContext).Assembly);

        ApplyUtcDateTimeConversion(modelBuilder);
    }

    /// <summary>
    /// Forces every <see cref="DateTime"/> property to UTC in both directions.
    /// </summary>
    /// <remarks>
    /// Npgsql maps <c>DateTime</c> to <c>timestamp with time zone</c> and throws on any value
    /// whose <see cref="DateTime.Kind"/> is not <see cref="DateTimeKind.Utc"/>. Reads come back
    /// as <c>Unspecified</c>, so without this a round-tripped entity would fail on its next save.
    /// Applying it centrally means no entity or caller has to remember.
    /// </remarks>
    private static void ApplyUtcDateTimeConversion(ModelBuilder modelBuilder)
    {
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            toDb => toDb.Kind == DateTimeKind.Utc ? toDb : toDb.ToUniversalTime(),
            fromDb => DateTime.SpecifyKind(fromDb, DateTimeKind.Utc));

        var nullableUtcConverter = new ValueConverter<DateTime?, DateTime?>(
            toDb => toDb.HasValue
                ? toDb.Value.Kind == DateTimeKind.Utc ? toDb : toDb.Value.ToUniversalTime()
                : toDb,
            fromDb => fromDb.HasValue
                ? DateTime.SpecifyKind(fromDb.Value, DateTimeKind.Utc)
                : fromDb);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        foreach (var property in entityType.GetProperties())
        {
            if (property.ClrType == typeof(DateTime))
                property.SetValueConverter(utcConverter);
            else if (property.ClrType == typeof(DateTime?))
                property.SetValueConverter(nullableUtcConverter);
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInformation();
        return base.SaveChanges();
    }

    /// <summary>
    /// Stamps audit timestamps centrally so no write path can forget them.
    /// </summary>
    private void ApplyAuditInformation()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    // CreatedAt is set once, at insert. Re-marking it unmodified stops a
                    // detached-entity update from overwriting it with a default value.
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    break;
            }
        }
    }
}
