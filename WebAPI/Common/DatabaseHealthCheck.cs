using MarwanQuTech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MarwanQuTech.WebApi.Common;

internal sealed class DatabaseHealthCheck(ApplicationDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
            await db.Database.CloseConnectionAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The exception goes to the server log only; the public /health body stays "Unhealthy".
            return HealthCheckResult.Unhealthy("Cannot reach SQL Server.", ex);
        }
    }
}
