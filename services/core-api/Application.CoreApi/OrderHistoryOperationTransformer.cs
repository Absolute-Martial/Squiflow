using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Application.CoreApi;

internal sealed class OrderHistoryOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!context.Description.ActionDescriptor.EndpointMetadata.OfType<EndpointAccessMetadata>()
            .Any(value => value.Access == EndpointAccess.AuthorizedTenantOrderHistory))
            return Task.CompletedTask;
        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "limit",
            In = ParameterLocation.Query,
            Required = false,
            Description = "Maximum number of full revision snapshots to return. Defaults to 5; cannot exceed 10.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Minimum = "1", Maximum = "10", Default = JsonValue.Create(5) },
        });
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "beforeRevision",
            In = ParameterLocation.Query,
            Required = false,
            Description = "Return revisions older than this positive revision. Use nextBeforeRevision from the previous page.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64", Minimum = "1" },
        });
        return Task.CompletedTask;
    }
}
