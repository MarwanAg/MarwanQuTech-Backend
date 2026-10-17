using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static MarwanQuTech.Domain.Common.DomainConstraints;

namespace MarwanQuTech.Infrastructure.Persistence.Configurations;

internal sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> b)
    {
        b.ToTable("Articles");
        b.HasKey(a => a.Id);
        b.Ignore(a => a.DomainEvents);

        b.ComplexProperty(a => a.Title, t => t.Localized("Title", Articles.TitleMaxLength));
        b.ComplexProperty(a => a.Summary, t => t.Localized("Summary", Articles.SummaryMaxLength));
        b.ComplexProperty(a => a.Content, t => t.Localized("Content", Articles.ContentMaxLength));

        b.ComplexProperty(a => a.Seo, seo =>
        {
            seo.ComplexProperty(s => s.Title, t => t.Localized("SeoTitle", Articles.SeoTitleMaxLength));
            seo.ComplexProperty(s => s.Description, t => t.Localized("SeoDescription", Articles.SeoDescriptionMaxLength));
        });

        b.Property(a => a.SlugAr).HasColumnName("Slug_AR").HasMaxLength(SlugMaxLength).IsRequired();
        b.Property(a => a.SlugEn).HasColumnName("Slug_EN").HasMaxLength(SlugMaxLength);
        b.Property(a => a.CoverImageUrl).HasMaxLength(UrlMaxLength);

        b.HasIndex(a => a.SlugAr).IsUnique();
        b.HasIndex(a => a.SlugEn).IsUnique().HasFilter("[Slug_EN] IS NOT NULL");
        b.HasIndex(a => new { a.IsPublished, a.PublishedAt });
    }
}
