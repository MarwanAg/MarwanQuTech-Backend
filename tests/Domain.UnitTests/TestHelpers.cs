using System.Reflection;
using MarwanQuTech.Domain.Common;

namespace MarwanQuTech.Domain.UnitTests;

internal static class TestHelpers
{
    public static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Simulates the Id that EF Core assigns after SaveChanges.</summary>
    public static T WithId<T>(this T entity, int id) where T : BaseEntity
    {
        typeof(BaseEntity)
            .GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, id);
        return entity;
    }
}
