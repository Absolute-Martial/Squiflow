using Application.CoreApi.Authorization;
using Application.IdentityAccess;
using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Application.CoreApi;

internal static class CoreApiApplicationAuthorizationMiddleware
{
    internal static IApplicationBuilder UseCoreApiApplicationAuthorization(this IApplicationBuilder application) =>
        application.Use(async (context, next) =>
        {
            var declaration = context.GetEndpoint()?.Metadata
                .GetOrderedMetadata<CoreApiApplicationAuthorizationMetadata>();
            if (declaration is null || declaration.Count == 0)
            {
                await next(context).ConfigureAwait(false);
                return;
            }
            if (declaration.Count != 1 || declaration[0].Requirements.Count == 0)
            {
                throw new InvalidOperationException(
                    "Authorized Core API execution requires exactly one validated application-authorization declaration.");
            }

            var rawTenantId = context.Request.RouteValues["tenantId"]?.ToString();
            if (!Guid.TryParse(rawTenantId, out var tenantId) || tenantId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "Authorized Core API execution requires the validated tenantId route value.");
            }

            var access = await TenantRequestAccess.ResolveAsync(
                tenantId,
                context,
                context.User,
                context.RequestServices.GetRequiredService<ResolveAccountBinding>(),
                context.RequestServices.GetRequiredService<ResolveTenantContext>(),
                context.RequestAborted).ConfigureAwait(false);
            if (access.Failure is not null)
            {
                await access.Failure.ExecuteAsync(context).ConfigureAwait(false);
                return;
            }

            var resource = CreateResource(
                declaration[0].Access,
                access.TenantContext!,
                context.RequestAborted);
            var authorization = context.RequestServices.GetRequiredService<IAuthorizationService>();
            var denied = await CoreApiDeclaredAuthorization.AuthorizeAsync(
                context,
                context.User,
                resource,
                authorization,
                "The account is not permitted to perform this operation in the requested tenant.")
                .ConfigureAwait(false);
            if (denied is not null)
            {
                await denied.ExecuteAsync(context).ConfigureAwait(false);
                return;
            }

            CoreApiDeclaredAuthorization.MarkMiddlewareAuthorized(context, declaration[0].Access);
            await next(context).ConfigureAwait(false);
        });

    private static object CreateResource(
        EndpointAccess access,
        TenantContext tenantContext,
        CancellationToken cancellationToken) => access switch
        {
            EndpointAccess.AuthorizedTenantWorkspace =>
                new TenantWorkspaceResource(tenantContext, cancellationToken),

            EndpointAccess.AuthorizedTenantOrderCreation or
            EndpointAccess.AuthorizedTenantOrderRead or
            EndpointAccess.AuthorizedTenantOrderBrowse or
            EndpointAccess.AuthorizedTenantOrderAbandon or
            EndpointAccess.AuthorizedTenantOrderCommit or
            EndpointAccess.AuthorizedTenantOrderRevision or
            EndpointAccess.AuthorizedTenantOrderPricePreview or
            EndpointAccess.AuthorizedTenantOrderHistory or
            EndpointAccess.AuthorizedTenantCatalogOrderCreation or
            EndpointAccess.AuthorizedTenantCatalogOrderRevision or
            EndpointAccess.AuthorizedTenantOrderActions =>
                new TenantOrderResource(tenantContext, cancellationToken),

            EndpointAccess.AuthorizedCustomerOrganizationCreation or
            EndpointAccess.AuthorizedCustomerOrganizationRead or
            EndpointAccess.AuthorizedCustomerOrganizationBrowse or
            EndpointAccess.AuthorizedCustomerProgramCreation or
            EndpointAccess.AuthorizedCustomerProgramRead or
            EndpointAccess.AuthorizedCustomerProgramBrowse or
            EndpointAccess.AuthorizedCustomerIndividualCreation or
            EndpointAccess.AuthorizedCustomerIndividualRead or
            EndpointAccess.AuthorizedCustomerIndividualAvailability or
            EndpointAccess.AuthorizedCustomerIndividualContactEdit or
            EndpointAccess.AuthorizedCustomerRepresentativeRead or
            EndpointAccess.AuthorizedCustomerRepresentativeManage or
            EndpointAccess.AuthorizedCustomerDuplicateRead or
            EndpointAccess.AuthorizedCustomerDuplicateResolve or
            EndpointAccess.AuthorizedCustomerDuplicateConsolidate or
            EndpointAccess.AuthorizedCustomerImport or
            EndpointAccess.AuthorizedCatalogRead or
            EndpointAccess.AuthorizedCatalogManage or
            EndpointAccess.AuthorizedPricingRead or
            EndpointAccess.AuthorizedPricingDraftEdit or
            EndpointAccess.AuthorizedPricingPublish or
            EndpointAccess.AuthorizedPricingRetire =>
                new TenantCustomerResource(tenantContext, cancellationToken),

            EndpointAccess.AuthorizedTenantRoleAdministration =>
                new TenantAuthorizationAdministrationResource(tenantContext, cancellationToken),

            _ => throw new InvalidOperationException(
                $"Core API access classification {access} has no application-authorization resource contract."),
        };
}
