using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Application.ApplicationEntry;
using Application.Web.Authentication;
using Microsoft.AspNetCore.Components.Authorization;

namespace Application.Web.Business;

public enum BusinessFailure { None, SignInRequired, Denied, NotFound, InvalidRequest, Conflict, Unavailable, TimedOut, InvalidResponse, Unknown }

public sealed record BusinessResult<T>(T? Value, BusinessFailure Failure, bool Replayed = false, bool Accepted = false) where T : class
{
    public bool Unknown => Failure == BusinessFailure.Unknown;
    public string Message => Failure switch
    {
        BusinessFailure.None when Accepted => "Request accepted for processing. Review the saved operation before another action.",
        BusinessFailure.None => Replayed ? "The earlier request was already saved. Its original receipt is shown." : "Request completed.",
        BusinessFailure.SignInRequired => "Your session has ended. Sign in again, then check the current record before sending another request.",
        BusinessFailure.Denied => "You do not have access to this operation. Each operation needs its own permission.",
        BusinessFailure.NotFound => "This record is unavailable in the current workspace. Check its reference or refresh the list.",
        BusinessFailure.InvalidRequest => "The request was rejected. Check the entered values and try again.",
        BusinessFailure.Conflict => "The request conflicts with current records. Reload the record and review it before trying again.",
        BusinessFailure.Unknown => "The save outcome is unknown. Retry the saved request to retrieve its receipt. Do not create a replacement request.",
        BusinessFailure.InvalidResponse => "The service returned an unsupported response. Try loading the record again.",
        BusinessFailure.TimedOut => "The service took too long to respond. Try loading the record again.",
        _ => "The service is unavailable. Try again when it is ready.",
    };
}

public sealed class BusinessCommand
{
    private BusinessCommand(Guid tenantId, string resource, byte[] body, HttpMethod method)
    {
        TenantId = tenantId;
        Resource = resource;
        Body = body;
        Method = method;
        Key = Guid.NewGuid().ToString("N");
    }

    public Guid TenantId { get; }
    public string Resource { get; }
    public string Key { get; }
    public HttpMethod Method { get; }
    internal byte[] Body { get; }
    private string? session;

    internal bool BindSession(string key) => string.Equals(Interlocked.CompareExchange(ref session, key, null) ?? key, key, StringComparison.Ordinal);

    public static BusinessCommand Create(Guid tenantId, string resource, object payload, HttpMethod? method = null)
    {
        if (method is not null && method != HttpMethod.Post && method != HttpMethod.Put) throw new ArgumentException("Unsupported business method.", nameof(method));
        BusinessApiClient.RequirePath(tenantId, resource);
        var body = JsonSerializer.SerializeToUtf8Bytes(payload, BusinessApiClient.JsonOptions);
        if (body.Length > (resource == "quotations" || resource.StartsWith("quotations/", StringComparison.Ordinal) ? BusinessApiClient.MaximumQuotationRequestBytes : BusinessApiClient.MaximumRequestBytes)) throw new ArgumentException("The request exceeds the supported size.", nameof(payload));
        return new(tenantId, resource, body, method ?? HttpMethod.Post);
    }
}

public sealed class BusinessApiClient(HttpClient http, Uri origin, AuthenticationStateProvider authentication, WebSessionTickets tickets)
{
    public const int MaximumRequestBytes = 8192;
    public const int MaximumQuotationRequestBytes = 64 * 1024;
    public const int MaximumResponseBytes = 256 * 1024;
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { MaxDepth = 16 };

    public Task<BusinessResult<T>> GetAsync<T>(Guid tenantId, string resource, Func<T, bool> validate,
        CancellationToken cancellationToken = default) where T : class =>
        RequestAsync(tenantId, resource, null, false, validate, cancellationToken);

    public Task<BusinessResult<T>> SendAsync<T>(BusinessCommand command, Func<T, bool> validate,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(command);
        return RequestAsync(command.TenantId, command.Resource, command, true, validate, cancellationToken);
    }

    public Task<BusinessResult<T>> QueryAsync<T>(Guid tenantId, string resource, object payload, Func<T, bool> validate,
        CancellationToken cancellationToken = default) where T : class =>
        RequestAsync(tenantId, resource, BusinessCommand.Create(tenantId, resource, payload), false, validate, cancellationToken);

    private async Task<BusinessResult<T>> RequestAsync<T>(Guid tenantId, string resource, BusinessCommand? command, bool mutation,
        Func<T, bool> validate, CancellationToken cancellationToken) where T : class
    {
        RequirePath(tenantId, resource);
        if (!ServiceAddress.TryParse(origin.AbsoluteUri, out _)) throw new InvalidOperationException("A safe CoreApi origin is required.");
        var state = await authentication.GetAuthenticationStateAsync();
        var session = state.User.FindFirst(WebSessionTickets.SessionClaim)?.Value;
        var token = tickets.AccessToken(session);
        if (string.IsNullOrWhiteSpace(token) || token.Length > 16 * 1024 || token.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            return new(null, BusinessFailure.SignInRequired);
        if (mutation && !command!.BindSession(session!)) return new(null, BusinessFailure.SignInRequired);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            using var request = new HttpRequestMessage(command is null ? HttpMethod.Get : command.Method,
                new Uri(origin, $"/api/v1/tenants/{tenantId:D}/{resource}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (command is not null)
            {
                if (mutation) request.Headers.Add("Idempotency-Key", command.Key);
                request.Content = new ByteArrayContent(command.Body);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budget.Token).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (tickets.AccessToken(session) is null) return new(null, !mutation ? BusinessFailure.SignInRequired : BusinessFailure.Unknown);
            var failure = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => BusinessFailure.SignInRequired,
                HttpStatusCode.Forbidden => BusinessFailure.Denied,
                HttpStatusCode.NotFound => BusinessFailure.NotFound,
                HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.UnprocessableEntity => BusinessFailure.InvalidRequest,
                HttpStatusCode.Conflict => BusinessFailure.Conflict,
                HttpStatusCode.OK or HttpStatusCode.Created => BusinessFailure.None,
                HttpStatusCode.Accepted when command is not null && resource.StartsWith("authorization/", StringComparison.Ordinal) && resource != "authorization/owner-transfer" => BusinessFailure.None,
                _ => !mutation ? BusinessFailure.Unavailable : BusinessFailure.Unknown,
            };
            if (failure != BusinessFailure.None) return new(null, failure);
            if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
                return new(null, !mutation ? BusinessFailure.InvalidResponse : BusinessFailure.Unknown);
            var bytes = await ReadBoundedAsync(response.Content, budget.Token).ConfigureAwait(false);
            var value = JsonSerializer.Deserialize<T>(bytes, JsonOptions);
            cancellationToken.ThrowIfCancellationRequested();
            if (tickets.AccessToken(session) is null) return new(null, !mutation ? BusinessFailure.SignInRequired : BusinessFailure.Unknown);
            if (value is null || !validate(value)) return new(null, !mutation ? BusinessFailure.InvalidResponse : BusinessFailure.Unknown);
            return new(value, BusinessFailure.None, response.Headers.TryGetValues("Idempotency-Replayed", out var replay) && replay.SequenceEqual(["true"]), response.StatusCode == HttpStatusCode.Accepted);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(null, !mutation ? BusinessFailure.TimedOut : BusinessFailure.Unknown); }
        catch (HttpRequestException) { return new(null, !mutation ? BusinessFailure.Unavailable : BusinessFailure.Unknown); }
        catch (IOException) { return new(null, !mutation ? BusinessFailure.Unavailable : BusinessFailure.Unknown); }
        catch (JsonException) { return new(null, !mutation ? BusinessFailure.InvalidResponse : BusinessFailure.Unknown); }
        catch (InvalidDataException) { return new(null, !mutation ? BusinessFailure.InvalidResponse : BusinessFailure.Unknown); }
    }

    internal static void RequirePath(Guid tenantId, string resource)
    {
        if (tenantId == Guid.Empty || string.IsNullOrEmpty(resource) || resource.Length > 1024 ||
            !(resource == "authorization" || resource.StartsWith("authorization/", StringComparison.Ordinal) || resource == "profile-policy" || resource == "profile-policy/publish" || resource == "quotations" || resource.StartsWith("quotations/", StringComparison.Ordinal) || resource == "order-approvals" || resource.StartsWith("order-approvals?", StringComparison.Ordinal) || resource.StartsWith("order-approvals/", StringComparison.Ordinal) || resource.StartsWith("customers/", StringComparison.Ordinal) || resource.StartsWith("catalog/", StringComparison.Ordinal) || resource.StartsWith("pricing/", StringComparison.Ordinal) || resource == "orders" || resource.StartsWith("orders?", StringComparison.Ordinal) || resource.StartsWith("orders/", StringComparison.Ordinal) || resource.StartsWith("invoices/", StringComparison.Ordinal) ||
                resource == "receivables" || resource.StartsWith("receivables?", StringComparison.Ordinal) || resource.StartsWith("receivables/", StringComparison.Ordinal) ||
                resource == "customer-payments" || resource.StartsWith("customer-payments?", StringComparison.Ordinal) || resource.StartsWith("customer-payments/", StringComparison.Ordinal) ||
                resource.StartsWith("debtors/", StringComparison.Ordinal) || resource == "fulfillment" || resource.StartsWith("fulfillment?", StringComparison.Ordinal) || resource.StartsWith("purchasing/", StringComparison.Ordinal)) ||
            resource.Contains("..", StringComparison.Ordinal) || resource.Contains("//", StringComparison.Ordinal) ||
            resource.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('/' or '-' or '_' or '?' or '=' or '&')))
            throw new ArgumentException("A bounded tenant business path is required.", nameof(resource));
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaximumResponseBytes) throw new InvalidDataException();
        await using var source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int count;
        while ((count = await source.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (buffer.Length + count > MaximumResponseBytes) throw new InvalidDataException();
            buffer.Write(chunk, 0, count);
        }
        return buffer.ToArray();
    }
}
