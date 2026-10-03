using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace Application.CoreApi;

// Body metadata is documentation only. Parsing remains behind current identity and permission
// checks, so endpoint selection cannot reveal a media-type error before authentication.
internal sealed class CustomerIndividualBodyTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var access = context.Description.ActionDescriptor.EndpointMetadata.OfType<EndpointAccessMetadata>().SingleOrDefault()?.Access;
        if (access is not (EndpointAccess.AuthorizedCustomerIndividualCreation or EndpointAccess.AuthorizedCustomerIndividualAvailability))
            return Task.CompletedTask;
        var create = access == EndpointAccess.AuthorizedCustomerIndividualCreation;
        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new()
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        AdditionalPropertiesAllowed = false,
                        Required = create ? new HashSet<string> { "displayName" } : new HashSet<string> { "expectedRevision", "availability" },
                        Properties = create ? new Dictionary<string, IOpenApiSchema>
                        {
                            ["displayName"] = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 1, MaxLength = 200 },
                            ["email"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, MaxLength = 254 },
                            ["phone"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, MaxLength = 32 },
                        } : new Dictionary<string, IOpenApiSchema>
                        {
                            ["expectedRevision"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64", Minimum = "1" },
                            ["availability"] = new OpenApiSchema { Type = JsonSchemaType.String, Enum = [JsonValue.Create("active")!, JsonValue.Create("inactive")!] },
                        },
                    },
                },
            },
        };
        return Task.CompletedTask;
    }
}
