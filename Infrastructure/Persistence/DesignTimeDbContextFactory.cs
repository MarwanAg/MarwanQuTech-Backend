using MediatR;
using Microsoft.EntityFrameworkCore.Design;

namespace MarwanQuTech.Infrastructure.Persistence;

/// <summary>
/// Used only by `dotnet ef` (migrations). Connection string comes from the
/// MARWANQUTECH_DESIGN_SQL environment variable, else LocalDB.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private const string DefaultConnection =
        @"Server=(localdb)\MSSQLLocalDB;Database=MarwanQuTech;Trusted_Connection=True;TrustServerCertificate=True";

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("MARWANQUTECH_DESIGN_SQL") ?? DefaultConnection;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name))
            .Options;

        return new ApplicationDbContext(options, new NoOpPublisher());
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
