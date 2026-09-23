namespace Application.CoreApi;

internal enum EndpointAccess
{
    PublicApplicationBootstrap,
    PublicApiDescription,
    PublicLiveness,
    AuthenticatedAccount,
    AuthenticatedTenantMemberships,
    AuthorizedTenantWorkspace,
    AuthorizedTenantOrderCreation,
    AuthorizedTenantOrderRead,
    AuthorizedTenantOrderBrowse,
    AuthorizedTenantOrderAbandon,
}

internal sealed record EndpointAccessMetadata(EndpointAccess Access);
