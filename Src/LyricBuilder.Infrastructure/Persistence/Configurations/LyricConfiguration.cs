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

        builder.Property(l => l.Concept)
            .HasMaxLength(2000);

        builder.Property(l => l.Language)
            .HasMaxLength(16);

        builder.Property(l => l.OriginalArtist)
            .HasMaxLength(200);

        builder.Property(l => l.Kind)
            .HasConversion<int>();

        // A style in use cannot be removed out from under its lyrics.
        builder.HasOne(l => l.Style)
            .WithMany()
            .HasForeignKey(l => l.StyleId)
            .OnDelete(DeleteBehavior.Restrict);

        // A writer with lyrics cannot be hard-deleted out from under them; users soft-delete.
        builder.HasOne(l => l.Author)
            .WithMany(u => u.Lyrics)
            .HasForeignKey(l => l.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Tags)
            .WithMany(t => t.Lyrics)
            .UsingEntity(
                "LyricTags",
                tag => tag.HasOne(typeof(Tag)).WithMany().HasForeignKey("TagId"),
                lyric => lyric.HasOne(typeof(Lyric)).WithMany().HasForeignKey("LyricId"));

        builder.HasMany(l => l.Sections)
            .WithOne(s => s.Lyric)
            .HasForeignKey(s => s.LyricId)
            .OnDelete(DeleteBehavior.Cascade);

        // Sections are mutated only through Lyric's methods, which work on the private list.
        builder.Navigation(l => l.Sections)
            .HasField("_sections")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(l => l.AuthorId);

        // The lookup generation runs to find examples: reference lyrics of a given style.
        builder.HasIndex(l => new { l.Kind, l.StyleId });

        // Soft-deleted rows disappear from every query unless a caller opts out with
        // IgnoreQueryFilters().
        builder.HasQueryFilter(l => !l.IsDeleted);
    }
}
