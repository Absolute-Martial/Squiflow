namespace Application.CoreApi;

internal enum EndpointAccess
{
    PublicApplicationBootstrap,
    PublicApiDescription,
    PublicLiveness,
    PublicReadiness,
    AuthenticatedAccount,
    AuthenticatedTenantMemberships,
    AuthorizedTenantWorkspace,
    AuthorizedTenantOrderCreation,
    AuthorizedTenantOrderRead,
    AuthorizedTenantOrderBrowse,
    AuthorizedTenantOrderAbandon,
    AuthorizedTenantOrderRevision,
    AuthorizedCustomerOrganizationCreation,
    AuthorizedCustomerOrganizationRead,
    AuthorizedCustomerOrganizationBrowse,
    AuthorizedCustomerProgramCreation,
    AuthorizedCustomerProgramRead,
    AuthorizedCustomerProgramBrowse,
}

internal sealed record EndpointAccessMetadata(EndpointAccess Access);
