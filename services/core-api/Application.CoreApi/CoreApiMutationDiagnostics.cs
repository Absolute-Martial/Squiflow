using System.Diagnostics.Metrics;
using Application.Tenancy;

namespace Application.CoreApi;

internal enum CoreApiMutation
{
    CustomerOrganizationCreated,
    CustomerProgramCreated,
    CustomerIndividualCreated,
    CustomerIndividualAvailabilityChanged,
    OrderDraftCreated,
    OrderDraftRevised,
    OrderDraftAbandoned,
    OrderDraftCommitted,
}

// Observes completed application commands. This is best-effort telemetry, not durable audit authority.
internal sealed class CoreApiMutationDiagnostics
{
    internal const string MeterName = "Application.CoreApi.Mutations";
    private static readonly Action<ILogger, string, Guid, Guid, Guid, bool, string, Exception?> LogSuccess =
        LoggerMessage.Define<string, Guid, Guid, Guid, bool, string>(LogLevel.Information,
            new EventId(1002, "MutationSucceeded"),
            "Mutation {Operation} tenant {TenantId} account {AccountId} resource {ResourceId} replayed {Replayed} request {RequestId}");
    private readonly ILogger<CoreApiMutationDiagnostics> _logger;
    private readonly Counter<long> _outcomes;
    private readonly Counter<long> _failures;

    // The named counters have finite operation/outcome/channel tags; identifiers are log fields only.
    public CoreApiMutationDiagnostics(ILogger<CoreApiMutationDiagnostics> logger, IMeterFactory meterFactory)
    {
        _logger = logger;
        var meter = meterFactory.Create(MeterName);
        _outcomes = meter.CreateCounter<long>("application.core_api.mutation.outcomes", "{outcome}");
        _failures = meter.CreateCounter<long>("application.core_api.mutation.diagnostic_failures", "{failure}");
    }

    internal void RecordSuccess(CoreApiMutation operation, TenantContext context, Guid resourceId,
        bool replayed, string requestId)
    {
        var name = operation switch
        {
            CoreApiMutation.CustomerOrganizationCreated => "customer.organization.create",
            CoreApiMutation.CustomerProgramCreated => "customer.program.create",
            CoreApiMutation.CustomerIndividualCreated => "customer.individual.create",
            CoreApiMutation.CustomerIndividualAvailabilityChanged => "customer.individual.availability",
            CoreApiMutation.OrderDraftCreated => "order.draft.create",
            CoreApiMutation.OrderDraftRevised => "order.draft.revise",
            CoreApiMutation.OrderDraftAbandoned => "order.draft.abandon",
            CoreApiMutation.OrderDraftCommitted => "order.draft.commit",
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        // Diagnostic providers/listeners execute arbitrary callbacks. Their non-fatal failure must
        // not turn an already committed command into an HTTP error or prevent the other channel.
#pragma warning disable CA1031 // Isolate optional diagnostic sinks from completed business outcomes.
        try
        {
            LogSuccess(_logger, name, context.TenantId, context.AccountId, resourceId, replayed, requestId, null);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            RecordDiagnosticFailure("logging");
        }
        try
        {
            _outcomes.Add(1, new("operation", name), new("outcome", replayed ? "replayed" : "committed"));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            RecordDiagnosticFailure("metrics");
        }
#pragma warning restore CA1031
    }

    private void RecordDiagnosticFailure(string channel)
    {
#pragma warning disable CA1031 // Failure reporting is also optional and cannot recursively report itself.
        try { _failures.Add(1, new KeyValuePair<string, object?>("channel", channel)); }
        catch (Exception error) when (error is not OutOfMemoryException) { }
#pragma warning restore CA1031
    }
}
