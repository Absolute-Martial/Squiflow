using Application.CoreApi.Authorization;
using Microsoft.AspNetCore.Authorization;

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

internal sealed record CoreApiApplicationAuthorizationMetadata(
    EndpointAccess Access,
    IReadOnlyList<IAuthorizationRequirement> Requirements);

internal static class CoreApiApplicationAuthorizationContract
{
    internal static IReadOnlyList<IAuthorizationRequirement> RequirementsFor(EndpointAccess access) => access switch
    {
        EndpointAccess.AuthorizedTenantWorkspace => [ViewTenantWorkspaceRequirement.Instance],
        EndpointAccess.AuthorizedTenantOrderCreation => [CreateOrderRequirement.Instance, ApplyManualPriceRequirement.Instance],
        EndpointAccess.AuthorizedTenantOrderRead => [ViewOrdersRequirement.Instance],
        EndpointAccess.AuthorizedTenantOrderBrowse => [ViewOrdersRequirement.Instance],
        EndpointAccess.AuthorizedTenantOrderAbandon => [AbandonOrderRequirement.Instance],
        EndpointAccess.AuthorizedTenantOrderCommit => [CommitOrderRequirement.Instance],
        EndpointAccess.AuthorizedTenantOrderRevision => [EditOrderRequirement.Instance, ApplyManualPriceRequirement.Instance],
        EndpointAccess.AuthorizedTenantOrderPricePreview => [CreateOrderRequirement.Instance, ApplyManualPriceRequirement.Instance],
        EndpointAccess.AuthorizedTenantOrderHistory => [ViewOrdersRequirement.Instance],
        EndpointAccess.AuthorizedTenantOrderActions => [ViewOrdersRequirement.Instance],
        EndpointAccess.AuthorizedCustomerOrganizationCreation => [CreateOrganizationRequirement.Instance],
        EndpointAccess.AuthorizedCustomerOrganizationRead => [ViewOrganizationsRequirement.Instance],
        EndpointAccess.AuthorizedCustomerOrganizationBrowse => [ViewOrganizationsRequirement.Instance],
        EndpointAccess.AuthorizedCustomerProgramCreation => [CreateProgramRequirement.Instance],
        EndpointAccess.AuthorizedCustomerProgramRead => [ViewProgramsRequirement.Instance],
        EndpointAccess.AuthorizedCustomerProgramBrowse => [ViewProgramsRequirement.Instance],
        EndpointAccess.AuthorizedCustomerIndividualCreation => [CreateIndividualRequirement.Instance],
        EndpointAccess.AuthorizedCustomerIndividualRead => [ViewIndividualsRequirement.Instance],
        EndpointAccess.AuthorizedCustomerIndividualAvailability => [ChangeIndividualAvailabilityRequirement.Instance],
        _ => throw new InvalidOperationException(
            $"Core API access classification {access} has no application-authorization contract."),
    };
}

internal static class CoreApiEndpointAccessExtensions
{
    internal static TBuilder WithCoreApiAccess<TBuilder>(this TBuilder builder, EndpointAccess access) where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new EndpointAccessMetadata(access));
        return builder;
    }

    internal static TBuilder WithCoreApiApplicationAuthorization<TBuilder>(this TBuilder builder, EndpointAccess access) where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new CoreApiApplicationAuthorizationMetadata(
            access,
            CoreApiApplicationAuthorizationContract.RequirementsFor(access)));
        return builder;
    }
}
