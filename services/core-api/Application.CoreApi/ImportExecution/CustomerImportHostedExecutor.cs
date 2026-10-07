using Application.CoreApi.Authorization;
using Application.Customers;
using Npgsql;

namespace Application.CoreApi.ImportExecution;

internal sealed class CustomerImportHostedExecutor(
    IServiceScopeFactory scopes,
    CustomerImportExecutionConfiguration configuration,
    CustomerImportExecutionState state,
    ILogger<CustomerImportHostedExecutor> logger) : BackgroundService
{
    private readonly Guid _workerId = Guid.NewGuid();
    private static readonly Action<ILogger, string, Exception?> LogRetry = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(1, "CustomerImportExecutionRetry"),
        "Customer import execution deferred: {FailureCode}.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.Enabled) return;
        Guid? cursor = null;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    CustomerImportTenantPage page;
                    using (var deadline = Deadline(stoppingToken))
                    await using (var scope = scopes.CreateAsyncScope())
                    {
                        page = await scope.ServiceProvider.GetRequiredService<ICustomerImportWorkDiscovery>()
                            .DiscoverRunnableTenantsAsync(cursor, configuration.TenantPageSize, deadline.Token).ConfigureAwait(false);
                    }
                    stoppingToken.ThrowIfCancellationRequested();
                    state.SetAccepting(true);
                    foreach (var tenantId in page.TenantIds)
                    {
                        stoppingToken.ThrowIfCancellationRequested();
                        try
                        {
                            using var deadline = Deadline(stoppingToken);
                            await using var scope = scopes.CreateAsyncScope();
                            await scope.ServiceProvider.GetRequiredService<RunCustomerImportBatch>()
                                .ExecuteAsync(tenantId, _workerId, configuration.BatchRows, deadline.Token).ConfigureAwait(false);
                            var sourceRetirement = scope.ServiceProvider.GetService<ReconcileCustomerImportSource>();
                            if (sourceRetirement is not null)
                            {
                                await sourceRetirement.RetireOneExpiredAsync(
                                    tenantId, DateTimeOffset.UtcNow, deadline.Token).ConfigureAwait(false);
                            }
                        }
                        catch (Exception exception) when (IsRetryable(exception, stoppingToken))
                        {
                            // The runner drains/releases before returning. Never launch a detached
                            // retry, invent a row result, or log provider exception/source contents.
                            LogRetry(logger, FailureCode(exception), null);
                        }
                    }
                    cursor = page.NextTenantId;
                }
                catch (Exception exception) when (IsRetryable(exception, stoppingToken))
                {
                    state.SetAccepting(false);
                    LogRetry(logger, FailureCode(exception), null);
                }
                await Task.Delay(configuration.PollInterval, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception)
        {
            // StopHost remains the standard fatal-error policy, without a provider/source
            // exception escaping into generic BackgroundService diagnostics.
            throw new InvalidOperationException("Customer import executor stopped after an unexpected failure.");
        }
        finally { state.BeginDrain(); }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        state.BeginDrain();
        // BackgroundService cancels and awaits ExecuteAsync. In-flight row/cleanup scopes are
        // not disposed until their awaited operations finish or the standard host drain expires.
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private CancellationTokenSource Deadline(CancellationToken stoppingToken)
    {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        deadline.CancelAfter(configuration.OperationTimeout);
        return deadline;
    }

    private static bool IsRetryable(Exception exception, CancellationToken stoppingToken) =>
        !stoppingToken.IsCancellationRequested
        && exception is AuthorizationProviderUnavailableException or NpgsqlException or OperationCanceledException;

    private static string FailureCode(Exception exception) => exception switch
    {
        AuthorizationProviderUnavailableException => "authorization_unavailable",
        OperationCanceledException => "operation_timeout",
        _ => "database_unavailable",
    };
}
