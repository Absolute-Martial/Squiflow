using Application.CoreApi.Authorization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;

namespace Application.CoreApi.Health;

internal sealed class OpenFgaReadinessCheck(
    IOpenFgaClient client,
    OpenFgaAuthorizationConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.ReadAuthorizationModel(
                new ClientReadAuthorizationModelOptions
                {
                    AuthorizationModelId = configuration.AuthorizationModelId,
                },
                cancellationToken);
            return string.Equals(
                response.AuthorizationModel?.Id,
                configuration.AuthorizationModelId,
                StringComparison.Ordinal)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Pinned authorization model unavailable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("Authorization provider unavailable.");
        }
    }
}
