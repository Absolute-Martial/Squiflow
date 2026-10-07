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

internal interface ITenantOrderAuthorization
{
    Task<bool> CanCreateAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);

    Task<bool> CanViewAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);

    Task<bool> CanAbandonAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);

    Task<bool> CanEditAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);

    Task<bool> CanApplyManualPriceAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);

    Task<bool> CanCommitAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
}

internal interface ITenantCustomerAuthorization
{
    Task<bool> CanCreateOrganizationAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CanViewOrganizationsAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CanCreateProgramAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CanViewProgramsAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CanCreateIndividualAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CanViewIndividualsAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CanChangeIndividualAvailabilityAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CanEditIndividualContactAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CanViewRepresentativesAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CanManageRepresentativesAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CanResolveCustomerDuplicatesAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CanConsolidateCustomerDuplicatesAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CanImportCustomersAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
}

internal interface ITenantCatalogAuthorization
{
    Task<bool> CanViewAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CanManageAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
}

internal interface ITenantPricingAuthorization
{
    Task<bool> CanViewAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CanEditDraftAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CanPublishAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CanRetireAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CanOverrideAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CanOverrideBeyondPolicyAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);
}

internal sealed class OpenFgaTenantAuthorization(
    IOpenFgaClient client,
    OpenFgaAuthorizationConfiguration configuration,
    ILogger<OpenFgaTenantAuthorization> logger) :
    ITenantWorkspaceAuthorization,
    ITenantOrderAuthorization,
    ITenantCustomerAuthorization,
    ITenantCatalogAuthorization,
    ITenantPricingAuthorization
{
    internal Task<bool> CheckQuotationAsync(Guid account, Guid tenant, Application.Quotations.QuotationCapability capability, CancellationToken ct) =>
        CheckAsync(account, tenant, capability switch
        {
            Application.Quotations.QuotationCapability.Create => "can_create_quotation",
            Application.Quotations.QuotationCapability.Edit => "can_edit_quotation",
            Application.Quotations.QuotationCapability.View => "can_view_quotations",
            Application.Quotations.QuotationCapability.Issue => "can_issue_quotation",
            _ => throw new ArgumentOutOfRangeException(nameof(capability)),
        }, ct);

    private const string MemberRelation = "member";
    private const string ViewWorkspaceRelation = "can_view_workspace";
    private const string CreateOrderRelation = "can_create_order";
    private const string ViewOrdersRelation = "can_view_orders";
    private const string AbandonOrderRelation = "can_abandon_order";
    private const string EditOrderRelation = "can_edit_order";
    private const string ApplyManualPriceRelation = "can_apply_manual_price";
    private const string CommitOrderRelation = "can_commit_order";
    private const string CreateOrganizationRelation = "can_create_organization";
    private const string ViewOrganizationsRelation = "can_view_organizations";
    private const string CreateProgramRelation = "can_create_program";
    private const string ViewProgramsRelation = "can_view_programs";
    private const string CreateIndividualRelation = "can_create_individual";
    private const string ViewIndividualsRelation = "can_view_individuals";
    private const string ChangeIndividualAvailabilityRelation = "can_change_individual_availability";
    private const string EditIndividualContactRelation = "can_edit_individual_contact";
    private const string ViewRepresentativesRelation = "can_view_representatives";
    private const string ManageRepresentativesRelation = "can_manage_representatives";
    private const string ResolveCustomerDuplicatesRelation = "can_resolve_customer_duplicates";
    private const string ConsolidateCustomerDuplicatesRelation = "can_consolidate_customer_duplicates";
    private const string ImportCustomersRelation = "can_import_customers";
    private const string ViewCatalogRelation = "can_view_catalog";
    private const string ManageCatalogRelation = "can_manage_catalog";
    private const string ViewPricingRelation = "can_view_pricing";
    private const string EditPricingDraftRelation = "can_edit_pricing_draft";
    private const string PublishPricingRelation = "can_publish_pricing";
    private const string RetirePricingRelation = "can_retire_pricing";
    private const string OverridePricingRelation = "can_override_pricing";
    private const string OverrideBeyondPolicyPricingRelation = "can_override_pricing_beyond_policy";
    private static readonly Meter Meter = new("Application.CoreApi.Authorization", "0.1.0");
    private static readonly Counter<long> Decisions = Meter.CreateCounter<long>("application.authorization.decisions");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "application.authorization.duration",
        "ms");
    private static readonly Action<ILogger, string, string, Guid, Guid, string, Exception?> LogDecision =
        LoggerMessage.Define<string, string, Guid, Guid, string>(
            LogLevel.Debug,
            new EventId(1, "TenantAuthorizationDecision"),
            "OpenFGA authorization returned {Decision} for relation {PermissionRelation}, account {AccountId}, and tenant {TenantId} using model {AuthorizationModelId}.");
    private static readonly Action<ILogger, string, Guid, Guid, string, Exception?> LogTimeout =
        LoggerMessage.Define<string, Guid, Guid, string>(
            LogLevel.Warning,
            new EventId(2, "TenantAuthorizationTimeout"),
            "OpenFGA authorization timed out for relation {PermissionRelation}, account {AccountId}, and tenant {TenantId} using model {AuthorizationModelId}.");
    private static readonly Action<ILogger, string, Guid, Guid, string, Exception?> LogUnavailable =
        LoggerMessage.Define<string, Guid, Guid, string>(
            LogLevel.Warning,
            new EventId(3, "TenantAuthorizationUnavailable"),
            "OpenFGA authorization was unavailable for relation {PermissionRelation}, account {AccountId}, and tenant {TenantId} using model {AuthorizationModelId}.");

    public async Task<bool> CanViewAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await CheckAsync(
                accountId,
                tenantId,
                ViewWorkspaceRelation,
                cancellationToken)
            .ConfigureAwait(false);

    Task<bool> ITenantOrderAuthorization.CanCreateAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, CreateOrderRelation, cancellationToken);

    Task<bool> ITenantOrderAuthorization.CanViewAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, ViewOrdersRelation, cancellationToken);

    Task<bool> ITenantOrderAuthorization.CanAbandonAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, AbandonOrderRelation, cancellationToken);

    Task<bool> ITenantOrderAuthorization.CanEditAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, EditOrderRelation, cancellationToken);

    Task<bool> ITenantOrderAuthorization.CanApplyManualPriceAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, ApplyManualPriceRelation, cancellationToken);

    Task<bool> ITenantOrderAuthorization.CanCommitAsync(
        Guid accountId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, CommitOrderRelation, cancellationToken);

    Task<bool> ITenantCustomerAuthorization.CanCreateOrganizationAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, CreateOrganizationRelation, cancellationToken);

    Task<bool> ITenantCustomerAuthorization.CanViewOrganizationsAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, ViewOrganizationsRelation, cancellationToken);

    Task<bool> ITenantCustomerAuthorization.CanCreateProgramAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, CreateProgramRelation, cancellationToken);

    Task<bool> ITenantCustomerAuthorization.CanViewProgramsAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, ViewProgramsRelation, cancellationToken);

    Task<bool> ITenantCustomerAuthorization.CanCreateIndividualAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, CreateIndividualRelation, cancellationToken);

    Task<bool> ITenantCustomerAuthorization.CanViewIndividualsAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, ViewIndividualsRelation, cancellationToken);

    Task<bool> ITenantCustomerAuthorization.CanChangeIndividualAvailabilityAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, ChangeIndividualAvailabilityRelation, cancellationToken);

    Task<bool> ITenantCustomerAuthorization.CanEditIndividualContactAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, EditIndividualContactRelation, cancellationToken);

    Task<bool> ITenantCustomerAuthorization.CanViewRepresentativesAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, ViewRepresentativesRelation, cancellationToken);

    Task<bool> ITenantCustomerAuthorization.CanManageRepresentativesAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, ManageRepresentativesRelation, cancellationToken);

    Task<bool> ITenantCustomerAuthorization.CanResolveCustomerDuplicatesAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, ResolveCustomerDuplicatesRelation, cancellationToken);

    Task<bool> ITenantCustomerAuthorization.CanConsolidateCustomerDuplicatesAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, ConsolidateCustomerDuplicatesRelation, cancellationToken);

    Task<bool> ITenantCustomerAuthorization.CanImportCustomersAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, ImportCustomersRelation, cancellationToken);

    Task<bool> ITenantCatalogAuthorization.CanViewAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, ViewCatalogRelation, cancellationToken);

    Task<bool> ITenantCatalogAuthorization.CanManageAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, ManageCatalogRelation, cancellationToken);

    Task<bool> ITenantPricingAuthorization.CanViewAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, ViewPricingRelation, cancellationToken);

    Task<bool> ITenantPricingAuthorization.CanEditDraftAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, EditPricingDraftRelation, cancellationToken);

    Task<bool> ITenantPricingAuthorization.CanPublishAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, PublishPricingRelation, cancellationToken);

    Task<bool> ITenantPricingAuthorization.CanRetireAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, RetirePricingRelation, cancellationToken);

    Task<bool> ITenantPricingAuthorization.CanOverrideAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, OverridePricingRelation, cancellationToken);

    Task<bool> ITenantPricingAuthorization.CanOverrideBeyondPolicyAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
        CheckAsync(accountId, tenantId, OverrideBeyondPolicyPricingRelation, cancellationToken);

    private async Task<bool> CheckAsync(
        Guid accountId,
        Guid tenantId,
        string permissionRelation,
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
                    Relation = permissionRelation,
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
            Decisions.Add(
                1,
                new KeyValuePair<string, object?>("decision", allowed ? "allow" : "deny"),
                new KeyValuePair<string, object?>("relation", permissionRelation));
            LogDecision(
                logger,
                allowed ? "allow" : "deny",
                permissionRelation,
                accountId,
                tenantId,
                configuration.AuthorizationModelId,
                null);
            return allowed;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            Decisions.Add(
                1,
                new KeyValuePair<string, object?>("decision", "unavailable"),
                new KeyValuePair<string, object?>("relation", permissionRelation));
            LogTimeout(
                logger,
                permissionRelation,
                accountId,
                tenantId,
                configuration.AuthorizationModelId,
                null);
            throw new AuthorizationProviderUnavailableException("The authorization provider timed out.", exception);
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException)
        {
            Decisions.Add(
                1,
                new KeyValuePair<string, object?>("decision", "unavailable"),
                new KeyValuePair<string, object?>("relation", permissionRelation));
            LogUnavailable(
                logger,
                permissionRelation,
                accountId,
                tenantId,
                configuration.AuthorizationModelId,
                null);
            throw new AuthorizationProviderUnavailableException("The authorization provider was unavailable.", exception);
        }
        finally
        {
            Duration.Record(
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                new KeyValuePair<string, object?>("relation", permissionRelation));
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
