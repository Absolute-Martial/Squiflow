using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Application.AdminApi;

internal enum AdminEndpointAuditOperation
{
    PlatformAccess,
    TenantProvision,
    AccountOnboard,
    IdentityLink,
    MembershipInvite,
    MembershipBootstrapOwner,
    MembershipTransition,
    TenantLifecycleTransition,
    RegistryTenantsBrowse,
    RegistryTenantDetail,
    RegistryAccountsBrowse,
    RegistryAccountDetail,
    RegistryMembershipsBrowse,
    RegistryMembershipDetail,
    TenantProfileAuthorityRead,
    TenantProfileRead,
    TenantProfilePublish,
    TenantProfileActivate,
    LegacyOrderProfileAssignment,
}

internal sealed record AdminEndpointAuditMetadata(AdminEndpointAuditOperation Operation);

internal static class AdminApiPlatformAuthorization
{
    private static readonly object AuthorizedAccessKey = new();

    internal static IApplicationBuilder UseAdminApiPlatformAuthorization(this IApplicationBuilder application) =>
        application.Use(async (context, next) =>
        {
            var endpoint = context.GetEndpoint();
            var accessMetadata = endpoint?.Metadata.GetOrderedMetadata<AdminEndpointAccessMetadata>();
            if (accessMetadata is null || accessMetadata.Count != 1 || !accessMetadata[0].IsProtected)
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            var auditMetadata = endpoint!.Metadata.GetOrderedMetadata<AdminEndpointAuditMetadata>();
            if (auditMetadata.Count != 1 || !Enum.IsDefined(auditMetadata[0].Operation))
            {
                throw new InvalidOperationException(
                    "Protected Admin API execution requires exactly one validated audit operation declaration.");
            }

            var authorizer = context.RequestServices.GetRequiredService<PlatformAdminRequestAuthorizer>();
            var authorization = await authorizer.AuthorizeAsync(
                context,
                FormatAuditOperation(auditMetadata[0].Operation, context)).ConfigureAwait(false);
            if (authorization.Access is null)
            {
                await (authorization.Failure ?? throw new InvalidOperationException(
                        "Denied Admin API authorization did not provide a failure result."))
                    .ExecuteAsync(context)
                    .ConfigureAwait(false);
                return;
            }

            context.Items[AuthorizedAccessKey] = authorization.Access;
            await next(context).ConfigureAwait(false);
        });

    internal static PlatformAdminRequestAccess GetRequiredAccess(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items.TryGetValue(AuthorizedAccessKey, out var value) &&
               value is PlatformAdminRequestAccess access
            ? access
            : throw new InvalidOperationException(
                "Protected Admin API execution requires completed platform authorization middleware.");
    }

    internal static string FormatAuditOperation(AdminEndpointAuditOperation operation, HttpContext context) => operation switch
    {
        AdminEndpointAuditOperation.PlatformAccess => "platform_access",
        AdminEndpointAuditOperation.TenantProvision => "tenant_provision",
        AdminEndpointAuditOperation.AccountOnboard => "account_onboard",
        AdminEndpointAuditOperation.IdentityLink => "identity_link",
        AdminEndpointAuditOperation.MembershipInvite => "membership_invite",
        AdminEndpointAuditOperation.MembershipBootstrapOwner => "membership_bootstrap_owner",
        AdminEndpointAuditOperation.MembershipTransition => $"membership_{KnownOperation(context, "operation", "activate", "suspend", "remove")}",
        AdminEndpointAuditOperation.TenantLifecycleTransition => $"tenant_{KnownOperation(context, "operation", "suspend", "reactivate")}",
        AdminEndpointAuditOperation.RegistryTenantsBrowse => "registry_tenants_browse",
        AdminEndpointAuditOperation.RegistryTenantDetail => $"registry_tenant_detail:{RouteGuid(context, "tenantId"):N}",
        AdminEndpointAuditOperation.RegistryAccountsBrowse => "registry_accounts_browse",
        AdminEndpointAuditOperation.RegistryAccountDetail => $"registry_account_detail:{RouteGuid(context, "accountId"):N}",
        AdminEndpointAuditOperation.RegistryMembershipsBrowse => $"registry_memberships_browse:{RouteGuid(context, "tenantId"):N}",
        AdminEndpointAuditOperation.RegistryMembershipDetail =>
            $"registry_membership_detail:{RouteGuid(context, "tenantId"):N}:{RouteGuid(context, "accountId"):N}",
        AdminEndpointAuditOperation.TenantProfileAuthorityRead =>
            $"tenant_profile_authority_read:{RouteGuid(context, "tenantId"):N}",
        AdminEndpointAuditOperation.TenantProfileRead =>
            $"tenant_profile_read:{RouteGuid(context, "tenantId"):N}:{RouteGuid(context, "profileId"):N}",
        AdminEndpointAuditOperation.TenantProfilePublish => $"tenant_profile_publish:{RouteGuid(context, "tenantId"):N}",
        AdminEndpointAuditOperation.TenantProfileActivate => $"tenant_profile_activate:{RouteGuid(context, "tenantId"):N}",
        AdminEndpointAuditOperation.LegacyOrderProfileAssignment =>
            $"legacy_order_profile_assignment:{RouteGuid(context, "tenantId"):N}:{RouteGuid(context, "orderId"):N}",
        _ => throw new InvalidOperationException("Unknown Admin API audit operation declaration."),
    };

    private static Guid RouteGuid(HttpContext context, string name)
    {
        var value = context.Request.RouteValues[name]?.ToString();
        return Guid.TryParse(value, out var parsed) && parsed != Guid.Empty
            ? parsed
            : throw new InvalidOperationException($"Protected Admin API route value '{name}' was not a valid identifier.");
    }

    private static string KnownOperation(HttpContext context, string name, params string[] allowed)
    {
        var value = context.Request.RouteValues[name]?.ToString();
        return value is not null && allowed.Contains(value, StringComparer.Ordinal)
            ? value
            : "unknown";
    }
}
