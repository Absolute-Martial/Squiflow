using System.Text.Json;
using Application.Orders;

namespace Application.CoreApi.Composition;

internal static class OrderProgramReferenceRoutes
{
    internal static WebApplication MapOrderProgramReferenceRoutes(this WebApplication app)
    {
        app.MapPut("/api/v1/tenants/{tenantId:guid}/orders/{orderId:guid}/program-reference",
            async (Guid tenantId, Guid orderId, HttpContext http, SetOrderProgramReference operation) =>
            {
                if (!TenantOrderEndpoint.TryGetIdempotencyKey(http.Request.Headers, out var key))
                    return TenantOrderEndpoint.InvalidRequest("idempotency_key_invalid", "An Idempotency-Key is required.");
                try
                {
                    if (!http.Request.HasJsonContentType()) throw new JsonException();
                    if (http.Request.ContentLength > 4096) return TenantOrderEndpoint.RequestTooLarge();
                    var bytes = new byte[4097];
                    var count = await http.Request.Body.ReadAtLeastAsync(bytes, bytes.Length, false, http.RequestAborted);
                    if (count > 4096) return TenantOrderEndpoint.RequestTooLarge();
                    using var document = JsonDocument.Parse(bytes.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 4 });
                    var body = document.RootElement;
                    if (body.ValueKind != JsonValueKind.Object) throw new ArgumentException("The Order reference request is invalid.");
                    var names = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var field in body.EnumerateObject())
                        if (!names.Add(field.Name) || field.Name is not ("expectedRevision" or "externalProgramReference")) throw new ArgumentException("The Order reference request is invalid.");
                    if (!body.TryGetProperty("expectedRevision", out var revision) || revision.ValueKind != JsonValueKind.Number || !revision.TryGetInt64(out var expectedRevision) ||
                        !body.TryGetProperty("externalProgramReference", out var value) || value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                        throw new ArgumentException("The Order reference request is invalid.");
                    var context = TenantRequestAccess.RequireResolvedContext(tenantId, http);
                    var result = await operation.ExecuteAsync(context, new(orderId, expectedRevision, ReadReference(value)), key!, http.RequestAborted);
                    if (result.Status is SetOrderProgramReferenceStatus.Updated or SetOrderProgramReferenceStatus.Replayed)
                    {
                        if (result.Order is not { } order || order.OrderId != orderId || order.TenantId != context.TenantId || order.ProgramPolicy is null)
                            throw new InvalidOperationException("The Order reference command returned inconsistent identity or policy facts.");
                        if (result.Status == SetOrderProgramReferenceStatus.Replayed) http.Response.Headers.Append("Idempotency-Replayed", "true");
                        return Results.Ok(new OrderProgramReferenceResponse(orderId, order.Revision, order.ExternalProgramReference, order.ProgramPolicy));
                    }
                    var code = result.Status switch
                    {
                        SetOrderProgramReferenceStatus.NotFound => "order_not_found",
                        SetOrderProgramReferenceStatus.RevisionConflict => "order_revision_conflict",
                        SetOrderProgramReferenceStatus.AlreadyCommitted => "order_already_committed",
                        SetOrderProgramReferenceStatus.AlreadyAbandoned => "order_already_abandoned",
                        SetOrderProgramReferenceStatus.IdempotencyKeyConflict => "idempotency_key_conflict",
                        SetOrderProgramReferenceStatus.ProfileUnavailable => "order_profile_unavailable",
                        _ => throw new InvalidOperationException("Unsupported Order reference outcome."),
                    };
                    return Results.Problem(statusCode: result.Status == SetOrderProgramReferenceStatus.NotFound ? 404 :
                        result.Status == SetOrderProgramReferenceStatus.ProfileUnavailable ? 503 : 409,
                        title: "The Order reference could not be changed.", extensions: new Dictionary<string, object?> { ["code"] = code });
                }
                catch (Exception error) when (error is ArgumentException or JsonException)
                {
                    return TenantOrderEndpoint.InvalidRequest("order_program_reference_invalid", "The Order reference request is invalid.");
                }
            }).RequireAuthorization().WithCoreApiAccess(EndpointAccess.AuthorizedTenantOrderProgramReference)
            .WithCoreApiApplicationAuthorization(EndpointAccess.AuthorizedTenantOrderProgramReference)
            .WithName("SetOrderProgramReference").WithTags("Orders")
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(4096))
            .Accepts<OrderProgramReferencePayload>("application/json")
            .Produces<OrderProgramReferenceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
        return app;
    }

    private static string? ReadReference(JsonElement value)
    {
        try { return value.ValueKind == JsonValueKind.Null ? null : value.GetString(); }
        catch (InvalidOperationException) { throw new ArgumentException("The Order reference text is invalid."); }
    }
}

internal sealed record OrderProgramReferencePayload(
    [property: System.Text.Json.Serialization.JsonRequired] long ExpectedRevision,
    [property: System.Text.Json.Serialization.JsonRequired] string? ExternalProgramReference);
internal sealed record OrderProgramReferenceResponse(Guid OrderId, long Revision, string? ExternalProgramReference, OrderProgramPolicyFacts ProgramPolicy);
