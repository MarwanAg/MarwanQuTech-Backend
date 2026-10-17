namespace MarwanQuTech.Domain.Common;

/// <summary>
/// Entity with creation / modification timestamps (UTC).
/// The values are filled automatically by an EF Core SaveChanges interceptor
/// in the Infrastructure layer — domain code never sets them by hand.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }
}
