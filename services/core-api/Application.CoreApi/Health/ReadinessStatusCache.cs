using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Application.CoreApi.Health;

// A public probe cannot be allowed to amplify into unbounded dependency traffic.
internal sealed class ReadinessStatusCache(
    HealthCheckService healthChecks,
    TimeProvider timeProvider) : IDisposable
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Retention = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Snapshot? _snapshot;

    internal async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot is not null && timeProvider.GetUtcNow() < snapshot.ValidUntil)
        {
            return snapshot.Healthy;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            snapshot = Volatile.Read(ref _snapshot);
            if (snapshot is not null && timeProvider.GetUtcNow() < snapshot.ValidUntil)
            {
                return snapshot.Healthy;
            }

            bool healthy;
            using var probeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            probeCancellation.CancelAfter(ProbeTimeout);
            try
            {
                var report = await healthChecks.CheckHealthAsync(
                    static registration => registration.Tags.Contains("readiness"),
                    probeCancellation.Token);
                healthy = report.Status == HealthStatus.Healthy;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                healthy = false;
            }
            catch (Exception)
            {
                healthy = false;
            }

            Volatile.Write(ref _snapshot, new Snapshot(
                healthy,
                timeProvider.GetUtcNow() + Retention));
            return healthy;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private sealed record Snapshot(bool Healthy, DateTimeOffset ValidUntil);
}
