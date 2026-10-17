using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static MarwanQuTech.Domain.Common.DomainConstraints;

namespace MarwanQuTech.Infrastructure.Persistence.Configurations;

internal sealed class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> b)
    {
        b.ToTable("AdminUsers");
        b.HasKey(a => a.Id);
        b.Ignore(a => a.DomainEvents);

        b.Property(a => a.Email).AsEmail();
        b.Property(a => a.DisplayName).HasMaxLength(AdminUsers.DisplayNameMaxLength).IsRequired();
        b.Property(a => a.PasswordHash).HasMaxLength(AdminUsers.PasswordHashMaxLength).IsRequired();

        b.HasIndex(a => a.Email).IsUnique();

        b.HasMany(a => a.RefreshTokens)
            .WithOne()
            .HasForeignKey(t => t.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Navigation(a => a.RefreshTokens).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens");
        b.HasKey(t => t.Id);
        b.Ignore(t => t.DomainEvents);

        b.Property(t => t.TokenHash).HasMaxLength(RefreshTokens.TokenHashMaxLength).IsRequired();
        b.Property(t => t.ReplacedByTokenHash).HasMaxLength(RefreshTokens.TokenHashMaxLength);

        b.HasIndex(t => t.TokenHash).IsUnique();
    }
}
