using LyricBuilder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LyricBuilder.Infrastructure.Persistence.Configurations;

public class LyricSectionConfiguration : IEntityTypeConfiguration<LyricSection>
{
    public void Configure(EntityTypeBuilder<LyricSection> builder)
    {
        builder.ToTable("LyricSections");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Type)
            .HasConversion<int>();

        builder.Property(s => s.Origin)
            .HasConversion<int>();

        builder.Property(s => s.Brief)
            .HasMaxLength(1000);

        builder.Property(s => s.Content)
            .IsRequired();

        builder.Property(s => s.RhymeScheme)
            .HasMaxLength(32);

        // Removing an original takes its repeats with it; Lyric.RemoveSection does the same in
        // memory, so this only matters for writes that bypass it.
        builder.HasOne<LyricSection>()
            .WithMany()
            .HasForeignKey(s => s.RepeatsSectionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Not unique: a move renumbers several rows in one save, and a unique index would
        // reject the intermediate states. Lyric keeps positions contiguous instead.
        builder.HasIndex(s => new { s.LyricId, s.Position });

        builder.HasIndex(s => s.Type);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
