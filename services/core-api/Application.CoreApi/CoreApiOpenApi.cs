using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;
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
            options.AddOperationTransformer<OrderCreateOperationTransformer>();
            options.AddOperationTransformer<OrderBrowseOperationTransformer>();
            options.AddOperationTransformer<OrderAbandonOperationTransformer>();
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

internal sealed class OrderCreateOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (!metadata.OfType<EndpointAccessMetadata>().Any(value =>
                value.Access == EndpointAccess.AuthorizedTenantOrderCreation))
        {
            return Task.CompletedTask;
        }

        AddRequiredIdempotencyKeyHeader(operation);
        AddReplayHeader(operation);
        AddCreatedLocationHeader(operation);
        return Task.CompletedTask;
    }

    internal static void AddRequiredIdempotencyKeyHeader(OpenApiOperation operation)
    {
        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Idempotency-Key",
            In = ParameterLocation.Header,
            Required = true,
            Description = "Provide one value from 1 to 128 characters. Reusing it with the same operation request replays the committed result.",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                MinLength = 1,
                MaxLength = 128,
            },
        });
    }

    internal static void AddReplayHeader(OpenApiOperation operation) =>
        AddResponseHeader(
            operation,
            StatusCodes.Status200OK,
            "Idempotency-Replayed",
            "Present with value true when the committed result is replayed for the supplied Idempotency-Key.");

    private static void AddCreatedLocationHeader(OpenApiOperation operation) =>
        AddResponseHeader(
            operation,
            StatusCodes.Status201Created,
            "Location",
            "URI of the created order draft.");

    private static void AddResponseHeader(
        OpenApiOperation operation,
        int statusCode,
        string headerName,
        string description)
    {
        var responses = operation.Responses
            ?? throw new InvalidOperationException("OpenAPI responses were not generated.");
        var response = responses[statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture)]
            as OpenApiResponse
            ?? throw new InvalidOperationException($"OpenAPI response {statusCode} was not generated.");
        response.Headers ??= new Dictionary<string, IOpenApiHeader>();
        response.Headers[headerName] = new OpenApiHeader
        {
            Description = description,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
            },
        };
    }
}

internal sealed class OrderAbandonOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Description.ActionDescriptor.EndpointMetadata
            .OfType<EndpointAccessMetadata>()
            .Any(value => value.Access == EndpointAccess.AuthorizedTenantOrderAbandon))
        {
            OrderCreateOperationTransformer.AddRequiredIdempotencyKeyHeader(operation);
            OrderCreateOperationTransformer.AddReplayHeader(operation);
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            Required = new HashSet<string> { "expectedRevision" },
                            Properties = new Dictionary<string, IOpenApiSchema>
                            {
                                ["expectedRevision"] = new OpenApiSchema
                                {
                                    Type = JsonSchemaType.Integer,
                                    Minimum = "1",
                                    Maximum = long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                },
                            },
                            AdditionalPropertiesAllowed = false,
                        },
                    },
                },
            };
        }

        return Task.CompletedTask;
    }
}

internal sealed class OrderBrowseOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (!metadata.OfType<EndpointAccessMetadata>().Any(value =>
                value.Access == EndpointAccess.AuthorizedTenantOrderBrowse))
        {
            return Task.CompletedTask;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "limit",
            In = ParameterLocation.Query,
            Required = false,
            Description = "Maximum number of drafts to return. Defaults to 25 and cannot exceed 50.",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.Integer,
                Minimum = "1",
                Maximum = "50",
                Default = JsonValue.Create(25),
            },
        });
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "after",
            In = ParameterLocation.Query,
            Required = false,
            Description = "Opaque tenant-bound cursor returned by the previous page. Do not construct or modify this value.",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                MaxLength = OrderDraftPageCursorCodec.MaximumEncodedLength,
            },
        });
        return Task.CompletedTask;
    }
}
