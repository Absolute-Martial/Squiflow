using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Authorization;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Exceptions;
using OpenFga.Sdk.Model;
using Application.Tenancy;

namespace Application.CoreApi.Authorization;

internal interface ITenantWorkspaceAuthorization
{
    Task<bool> CanViewAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
}

internal sealed class OpenFgaTenantWorkspaceAuthorization(
    IOpenFgaClient client,
    OpenFgaAuthorizationConfiguration configuration,
    ILogger<OpenFgaTenantWorkspaceAuthorization> logger) : ITenantWorkspaceAuthorization
{
    private const string MemberRelation = "member";
    private const string ViewWorkspaceRelation = "can_view_workspace";
    private static readonly Meter Meter = new("Application.CoreApi.Authorization", "0.1.0");
    private static readonly Counter<long> Decisions = Meter.CreateCounter<long>("application.authorization.decisions");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "application.authorization.duration",
        "ms");
    private static readonly Action<ILogger, string, Guid, Guid, string, Exception?> LogDecision =
        LoggerMessage.Define<string, Guid, Guid, string>(
            LogLevel.Debug,
            new EventId(1, "TenantWorkspaceAuthorizationDecision"),
            "OpenFGA tenant-workspace authorization returned {Decision} for account {AccountId} and tenant {TenantId} using model {AuthorizationModelId}.");
    private static readonly Action<ILogger, Guid, Guid, string, Exception?> LogTimeout =
        LoggerMessage.Define<Guid, Guid, string>(
            LogLevel.Warning,
            new EventId(2, "TenantWorkspaceAuthorizationTimeout"),
            "OpenFGA tenant-workspace authorization timed out for account {AccountId} and tenant {TenantId} using model {AuthorizationModelId}.");
    private static readonly Action<ILogger, Guid, Guid, string, Exception?> LogUnavailable =
        LoggerMessage.Define<Guid, Guid, string>(
            LogLevel.Warning,
            new EventId(3, "TenantWorkspaceAuthorizationUnavailable"),
            "OpenFGA tenant-workspace authorization was unavailable for account {AccountId} and tenant {TenantId} using model {AuthorizationModelId}.");

    public async Task<bool> CanViewAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(accountId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);

        var started = Stopwatch.GetTimestamp();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(configuration.RequestTimeout);

        try
        {
            var user = $"user:{accountId:N}";
            var tenant = $"tenant:{tenantId:N}";
            var response = await client.Check(
                new ClientCheckRequest
                {
                    User = user,
                    Relation = ViewWorkspaceRelation,
                    Object = tenant,
                    ContextualTuples =
                    [
                        new ClientTupleKey
                        {
                            User = user,
                            Relation = MemberRelation,
                            Object = tenant,
                        },
                    ],
                },
                new ClientCheckOptions
                {
                    StoreId = configuration.StoreId,
                    AuthorizationModelId = configuration.AuthorizationModelId,
                    Consistency = ConsistencyPreference.HIGHERCONSISTENCY,
                },
                timeout.Token);

            var allowed = response.Allowed is true;
            Decisions.Add(1, new KeyValuePair<string, object?>("decision", allowed ? "allow" : "deny"));
            LogDecision(
                logger,
                allowed ? "allow" : "deny",
                accountId,
                tenantId,
                configuration.AuthorizationModelId,
                null);
            return allowed;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            Decisions.Add(1, new KeyValuePair<string, object?>("decision", "unavailable"));
            LogTimeout(
                logger,
                accountId,
                tenantId,
                configuration.AuthorizationModelId,
                null);
            throw new AuthorizationProviderUnavailableException("The authorization provider timed out.", exception);
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException)
        {
            Decisions.Add(1, new KeyValuePair<string, object?>("decision", "unavailable"));
            LogUnavailable(
                logger,
                accountId,
                tenantId,
                configuration.AuthorizationModelId,
                null);
            throw new AuthorizationProviderUnavailableException("The authorization provider was unavailable.", exception);
        }
        finally
        {
            Duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}

internal sealed class AuthorizationProviderUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);

internal sealed record TenantWorkspaceResource(
    TenantContext TenantContext,
    CancellationToken CancellationToken);

internal sealed class ViewTenantWorkspaceRequirement : IAuthorizationRequirement
{
    internal static ViewTenantWorkspaceRequirement Instance { get; } = new();

    private ViewTenantWorkspaceRequirement()
    {
    }
}

internal sealed class ViewTenantWorkspaceAuthorizationHandler(
    ITenantWorkspaceAuthorization authorization)
    : AuthorizationHandler<ViewTenantWorkspaceRequirement, TenantWorkspaceResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ViewTenantWorkspaceRequirement requirement,
        TenantWorkspaceResource resource)
    {
        if (context.User.Identity?.IsAuthenticated is not true)
        {
            return;
        }

        if (await authorization.CanViewAsync(
                resource.TenantContext.AccountId,
                resource.TenantContext.TenantId,
                resource.CancellationToken))
        {
            context.Succeed(requirement);
        }
    }
}
