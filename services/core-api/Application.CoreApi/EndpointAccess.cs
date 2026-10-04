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

    internal bool RequiresApplicationAuthorization =>
        IsProtected && Access is not EndpointAccess.AuthenticatedAccount and not EndpointAccess.AuthenticatedTenantMemberships;
}

internal sealed record CoreApiApplicationAuthorizationMetadata(EndpointAccess Access);

internal static class CoreApiEndpointAccessExtensions
{
    internal static TBuilder WithCoreApiAccess<TBuilder>(this TBuilder builder, EndpointAccess access) where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new EndpointAccessMetadata(access));
        return builder;
    }

    internal static TBuilder WithCoreApiApplicationAuthorization<TBuilder>(this TBuilder builder, EndpointAccess access) where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new CoreApiApplicationAuthorizationMetadata(access));
        return builder;
    }
}
