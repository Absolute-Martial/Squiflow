using System.Text.Json;

namespace Application.AdminApi;

internal static class BoundedAdminJson
{
    private const int MaximumRequestBodyBytes = 4096;

    internal static async Task<(JsonDocument? Document, IResult? Failure)> ReadObjectAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength is > MaximumRequestBodyBytes)
            return (null, Problem(413, "request_too_large", "The request body is too large."));
        if (!request.HasJsonContentType())
            return (null, Problem(415, "unsupported_media_type", "The request body must use application/json."));
        try
        {
            var bytes = new byte[MaximumRequestBodyBytes + 1];
            var received = 0;
            while (received < bytes.Length)
            {
                var read = await request.Body.ReadAsync(bytes.AsMemory(received), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                received += read;
            }
            if (received > MaximumRequestBodyBytes)
                return (null, Problem(413, "request_too_large", "The request body is too large."));
            var document = JsonDocument.Parse(bytes.AsMemory(0, received), new JsonDocumentOptions { MaxDepth = 2 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                return (null, Problem(400, "invalid_request", "The request body is invalid."));
            }
            return (document, null);
        }
        catch (Exception exception) when (exception is JsonException or BadHttpRequestException)
        {
            return (null, Problem(400, "invalid_request", "The request body is invalid."));
        }
    }

    internal static bool TrySingleGuid(JsonElement value, string name, out Guid result)
    {
        result = default;
        using var properties = value.EnumerateObject();
        return properties.MoveNext() &&
               properties.Current.NameEquals(name) &&
               properties.Current.Value.ValueKind == JsonValueKind.String &&
               Guid.TryParse(properties.Current.Value.GetString(), out result) &&
               result != Guid.Empty &&
               !properties.MoveNext();
    }

    internal static bool TrySinglePositiveInt(JsonElement value, string name, out int result)
    {
        result = default;
        using var properties = value.EnumerateObject();
        return properties.MoveNext() &&
               properties.Current.NameEquals(name) &&
               properties.Current.Value.TryGetInt32(out result) &&
               result > 0 &&
               !properties.MoveNext();
    }

    internal static IResult Problem(int status, string code, string title) =>
        Results.Problem(statusCode: status, title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
