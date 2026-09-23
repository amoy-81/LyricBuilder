using LyricBuilder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LyricBuilder.Infrastructure.Persistence.Configurations;

public class StyleConfiguration : IEntityTypeConfiguration<Style>
{
    public void Configure(EntityTypeBuilder<Style> builder)
    {
        builder.ToTable("Styles");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Description)
            .HasMaxLength(1000);

        builder.HasOne(s => s.ParentStyle)
            .WithMany(s => s.SubStyles)
            .HasForeignKey(s => s.ParentStyleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered so a soft-deleted style frees its slug.
        builder.HasIndex(s => s.Slug)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
