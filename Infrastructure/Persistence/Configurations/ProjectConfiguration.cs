using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static MarwanQuTech.Domain.Common.DomainConstraints;

namespace MarwanQuTech.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("Projects");
        b.HasKey(p => p.Id);
        b.Ignore(p => p.DomainEvents);

        b.ComplexProperty(p => p.Title, t => t.Localized("Title", Projects.TitleMaxLength));
        b.ComplexProperty(p => p.Summary, t => t.Localized("Summary", Projects.SummaryMaxLength));
        b.ComplexProperty(p => p.Description, t => t.Localized("Description", Projects.DescriptionMaxLength));

        b.Property(p => p.SlugAr).HasColumnName("Slug_AR").HasMaxLength(SlugMaxLength).IsRequired();
        b.Property(p => p.SlugEn).HasColumnName("Slug_EN").HasMaxLength(SlugMaxLength);

        b.Property(p => p.GithubUrl).HasMaxLength(UrlMaxLength);
        b.Property(p => p.LiveUrl).HasMaxLength(UrlMaxLength);
        b.Property(p => p.CoverImageUrl).HasMaxLength(UrlMaxLength);

        b.HasIndex(p => p.SlugAr).IsUnique();
        b.HasIndex(p => p.SlugEn).IsUnique().HasFilter("[Slug_EN] IS NOT NULL");
        b.HasIndex(p => new { p.IsPublished, p.DisplayOrder });
        b.HasIndex(p => new { p.IsPublished, p.IsFeatured });

        b.HasMany(p => p.Images)
            .WithOne()
            .HasForeignKey(i => i.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Navigation(p => p.Images).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ProjectImageConfiguration : IEntityTypeConfiguration<ProjectImage>
{
    public void Configure(EntityTypeBuilder<ProjectImage> b)
    {
        b.ToTable("ProjectImages");
        b.HasKey(i => i.Id);
        b.Ignore(i => i.DomainEvents);

        b.Property(i => i.ImageUrl).HasMaxLength(UrlMaxLength).IsRequired();
        b.ComplexProperty(i => i.AltText, t => t.Localized("AltText", ProjectImages.AltTextMaxLength));

        b.HasIndex(i => new { i.ProjectId, i.DisplayOrder });
    }
}
