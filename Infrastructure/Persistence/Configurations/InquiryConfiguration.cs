using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static MarwanQuTech.Domain.Common.DomainConstraints;

namespace MarwanQuTech.Infrastructure.Persistence.Configurations;

internal sealed class InquiryConfiguration : IEntityTypeConfiguration<Inquiry>
{
    public void Configure(EntityTypeBuilder<Inquiry> b)
    {
        b.ToTable("Inquiries");
        b.HasKey(i => i.Id);
        b.Ignore(i => i.DomainEvents);

        b.Property(i => i.ClientName).HasMaxLength(Inquiries.ClientNameMaxLength).IsRequired();
        b.Property(i => i.Email).AsEmail();
        b.Property(i => i.Phone).HasMaxLength(Inquiries.PhoneMaxLength);
        b.Property(i => i.Details).HasMaxLength(Inquiries.DetailsMaxLength).IsRequired();

        b.HasIndex(i => new { i.IsRead, i.CreatedAt });
        b.HasIndex(i => i.CreatedAt);
    }
}
