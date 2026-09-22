using LyricBuilder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LyricBuilder.Infrastructure.Persistence.Configurations;

public class LyricConfiguration : IEntityTypeConfiguration<Lyric>
{
    public void Configure(EntityTypeBuilder<Lyric> builder)
    {
        builder.ToTable("Lyrics");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(l => l.Content)
            .IsRequired();

        builder.Property(l => l.Genre)
            .HasMaxLength(100);

        builder.Property(l => l.Language)
            .HasMaxLength(16);

        builder.Property(l => l.Status)
            .HasConversion<int>();

        builder.HasIndex(l => l.AuthorId);
        builder.HasIndex(l => l.Status);

        // Soft-deleted rows disappear from every query unless a caller opts out with
        // IgnoreQueryFilters().
        builder.HasQueryFilter(l => !l.IsDeleted);
    }
}
