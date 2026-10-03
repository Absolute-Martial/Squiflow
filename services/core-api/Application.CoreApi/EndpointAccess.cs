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
    AuthorizedTenantOrderCommit,
    AuthorizedTenantOrderRevision,
    AuthorizedTenantOrderPricePreview,
    AuthorizedTenantOrderHistory,
    AuthorizedTenantOrderActions,
    AuthorizedCustomerOrganizationCreation,
    AuthorizedCustomerOrganizationRead,
    AuthorizedCustomerOrganizationBrowse,
    AuthorizedCustomerProgramCreation,
    AuthorizedCustomerProgramRead,
    AuthorizedCustomerProgramBrowse,
    AuthorizedCustomerIndividualCreation,
    AuthorizedCustomerIndividualRead,
    AuthorizedCustomerIndividualAvailability,
}

internal sealed record EndpointAccessMetadata(EndpointAccess Access)
{
    internal bool IsProtected => Access is not EndpointAccess.PublicApplicationBootstrap
        and not EndpointAccess.PublicApiDescription
        and not EndpointAccess.PublicLiveness
        and not EndpointAccess.PublicReadiness;
}
