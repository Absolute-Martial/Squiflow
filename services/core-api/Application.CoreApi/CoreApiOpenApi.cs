using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Application.Branding;

namespace Application.CoreApi;

internal static class CoreApiOpenApi
{
    internal const string DocumentName = "v1";
    internal const string SecuritySchemeName = "oidc";

    internal static IServiceCollection AddCoreApiOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer<ApiIdentityDocumentTransformer>();
            options.AddOperationTransformer<ProtectedOperationSecurityTransformer>();
        });

        return services;
    }
}

internal sealed class ApiIdentityDocumentTransformer(
    BrandProfile brand,
    OidcAuthenticationConfiguration authentication) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        document.Info = new OpenApiInfo
        {
            Title = $"{brand.DisplayName} API",
            Version = CoreApiOpenApi.DocumentName,
            Description = "Public application and authenticated account API.",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[CoreApiOpenApi.SecuritySchemeName] =
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OpenIdConnect,
                OpenIdConnectUrl = new Uri(
                    $"{authentication.Authority.TrimEnd('/')}/.well-known/openid-configuration",
                    UriKind.Absolute),
                Description = "OpenID Connect access token issued by the configured identity authority.",
            };

        return Task.CompletedTask;
    }
}

internal sealed class ProtectedOperationSecurityTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var requiresAuthorization = metadata.OfType<IAuthorizeData>().Any() &&
            !metadata.OfType<IAllowAnonymous>().Any();

        if (!requiresAuthorization)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(
                CoreApiOpenApi.SecuritySchemeName,
                context.Document,
                externalResource: null)] = [],
        });

        return Task.CompletedTask;
    }
}
