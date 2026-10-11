using System.Diagnostics.Metrics;
using Application.Customers;

namespace Application.CoreApi.ImportExecution;

internal sealed record CustomerImportRecoveryObservation(long Retired, long Deferred, long Failures);

// Observational only: no store, authority or recovery command is available here.
internal sealed class CustomerImportRecoveryMonitor : IDisposable
{
    internal const string MeterName = "Application.CustomerImportRecovery";
    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _outcomes;
    private long _retired, _deferred, _failures;

    public CustomerImportRecoveryMonitor() => _outcomes = _meter.CreateCounter<long>("customer_import_recovery_outcomes");

    internal CustomerImportRecoveryObservation Read() => new(
        Interlocked.Read(ref _retired), Interlocked.Read(ref _deferred), Interlocked.Read(ref _failures));

    internal void Observe(CustomerImportSourceRecoveryOutcome outcome)
    {
        if (outcome == CustomerImportSourceRecoveryOutcome.Retired) Interlocked.Increment(ref _retired);
        if (outcome == CustomerImportSourceRecoveryOutcome.Deferred) Interlocked.Increment(ref _deferred);
        Record(outcome switch
        {
            CustomerImportSourceRecoveryOutcome.Retired => "retired",
            CustomerImportSourceRecoveryOutcome.Deferred => "deferred",
            _ => "no_work",
        });
    }

    internal void ObserveFailure()
    {
        Interlocked.Increment(ref _failures);
        Record("failed");
    }

    private void Record(string outcome)
    {
        // A diagnostic subscriber is arbitrary external code. It cannot roll back a
        // committed retirement, prevent the next recovery, or stop this hosted executor.
        try { _outcomes.Add(1, new KeyValuePair<string, object?>("outcome", outcome)); }
        catch (Exception) { }
    }

    public void Dispose() => _meter.Dispose();
}
