using Microsoft.Extensions.Options;

namespace SquiFlow.CoreApi.Composition;

internal sealed class ProfileRuntimeMaintenanceService(
    ProfileRuntimeRegistry registry,
    IOptions<ProfileRuntimeOptions> options,
    TimeProvider timeProvider,
    ILogger<ProfileRuntimeMaintenanceService> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> MaintenanceFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(7110, nameof(MaintenanceFailed)),
            "Profile runtime idle-retirement maintenance failed.");

    private static readonly Action<ILogger, TimeSpan, Exception?> ShutdownDrainTimedOut =
        LoggerMessage.Define<TimeSpan>(
            LogLevel.Warning,
            new EventId(7111, nameof(ShutdownDrainTimedOut)),
            "Profile runtime hosted-service drain exceeded {ShutdownDrainTimeout}; container disposal will apply the forced-close policy.");

    private readonly ProfileRuntimeOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            _options.MaintenanceInterval,
            timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await registry
                    .RetireIdleAsync(timeProvider.GetUtcNow(), stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                MaintenanceFailed(logger, exception);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        using var drain = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        drain.CancelAfter(_options.ShutdownDrainTimeout);
        try
        {
            await registry.StopAsync(drain.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            drain.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            ShutdownDrainTimedOut(logger, _options.ShutdownDrainTimeout, null);
        }
    }
}
