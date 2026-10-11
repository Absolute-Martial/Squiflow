using Application.CoreApi.ImportExecution;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Application.CoreApi.Health;

// Retained raw customer PII is deleted only by the customer import executor. A disabled,
// still-starting, failed-discovery or draining executor is therefore a retention exposure, not
// a healthy-but-quiet dependency, and readiness must say so. Execution-disabled hosts retain
// nothing and never register this check.
internal sealed class CustomerImportExecutionReadinessCheck(
    CustomerImportExecutionConfiguration configuration,
    CustomerImportExecutionState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.Enabled) return Task.FromResult(HealthCheckResult.Healthy());
        return Task.FromResult(state.IsAcceptingWork
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy(
                "Customer import execution is not admitting work; retained raw import sources are not being deleted."));
    }
}
