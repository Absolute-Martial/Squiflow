using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Application.CoreApi.Health;

internal sealed class PrimaryDatabaseReadinessCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is int value && value == 1
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Primary database readiness query failed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("Primary database unavailable.");
        }
    }
}
