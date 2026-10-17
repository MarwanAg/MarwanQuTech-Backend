using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MarwanQuTech.Infrastructure.Persistence.Configurations;

internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

internal static class ModelBuilderExtensions
{
    /// <summary>Maps a <see cref="LocalizedText"/> to "{prefix}_AR" / "{prefix}_EN" columns.</summary>
    public static void Localized(
        this Microsoft.EntityFrameworkCore.Metadata.Builders.ComplexPropertyBuilder<LocalizedText> builder,
        string prefix,
        int maxLength)
    {
        var ar = builder.Property(t => t.Ar).HasColumnName($"{prefix}_AR").IsRequired();
        var en = builder.Property(t => t.En).HasColumnName($"{prefix}_EN").IsRequired();

        // nvarchar(n) is limited to 4000; longer texts become nvarchar(max). Limits are still enforced by the domain.
        if (maxLength <= 4000)
        {
            ar.HasMaxLength(maxLength);
            en.HasMaxLength(maxLength);
        }
    }

    public static Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<EmailAddress> AsEmail(
        this Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<EmailAddress> builder)
        => builder
            .HasConversion(e => e.Value, v => EmailAddress.Create(v))
            .HasMaxLength(DomainConstraints.EmailMaxLength)
            .IsRequired();
}
