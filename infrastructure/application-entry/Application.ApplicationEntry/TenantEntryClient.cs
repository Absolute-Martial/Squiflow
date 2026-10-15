using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Application.ApplicationEntry;

public sealed record AccountEntry(Guid AccountId);
public sealed record TenantEntry(Guid TenantId, string DisplayName);
public sealed record TenantEntryPage(TenantEntry[] Items, Guid? NextTenantId);
public enum TenantEntryFailure { None, SignInRequired, AccountUnavailable, TenantDenied, Unavailable, TimedOut, InvalidResponse }
public sealed record TenantEntryResult<T>(T? Value, TenantEntryFailure Failure) where T : class;

public sealed class TenantEntryClient(HttpClient http)
{
    public const int MaximumResponseBytes = 128 * 1024;
    public const int MaximumPageCount = 50;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { MaxDepth = 16 };

    public Task<TenantEntryResult<AccountEntry>> AccountAsync(Uri origin, string accessToken, CancellationToken cancellationToken = default) =>
        GetAsync<AccountEntry>(origin, "/api/v1/account", accessToken, value => value.AccountId != Guid.Empty, cancellationToken);

    public Task<TenantEntryResult<TenantEntryPage>> TenantsAsync(Uri origin, string accessToken, Guid? afterTenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (afterTenantId == Guid.Empty) throw new ArgumentException("Continuation identity cannot be empty.", nameof(afterTenantId));
        var path = "/api/v1/account/tenants/page" + (afterTenantId.HasValue ? $"?afterTenantId={afterTenantId.Value:D}" : "");
        return GetAsync<TenantEntryPage>(origin, path, accessToken, value =>
            value.Items is { Length: <= MaximumPageCount } && value.Items.All(ValidTenant) &&
            value.Items.Select(item => item.TenantId).Distinct().Count() == value.Items.Length &&
            (!value.NextTenantId.HasValue || value.Items.Length == MaximumPageCount && value.NextTenantId == value.Items[^1].TenantId) &&
            (!afterTenantId.HasValue || value.Items.All(item => item.TenantId != afterTenantId)), cancellationToken);
    }

    public Task<TenantEntryResult<TenantEntry>> WorkspaceAsync(Uri origin, string accessToken, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        return GetAsync<TenantEntry>(origin, $"/api/v1/tenants/{tenantId:D}/workspace", accessToken,
            value => ValidTenant(value) && value.TenantId == tenantId, cancellationToken);
    }

    private static bool ValidTenant(TenantEntry? value) => value is not null && value.TenantId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(value.DisplayName) && value.DisplayName.Length <= 200 && !value.DisplayName.Any(char.IsControl);

    private async Task<TenantEntryResult<T>> GetAsync<T>(Uri origin, string path, string accessToken,
        Func<T, bool> validate, CancellationToken cancellationToken) where T : class
    {
        if (!ServiceAddress.TryParse(origin.AbsoluteUri, out _)) throw new ArgumentException("A safe service origin is required.", nameof(origin));
        if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Length > 16 * 1024 || accessToken.Any(char.IsWhiteSpace) || accessToken.Any(char.IsControl))
            throw new ArgumentException("A bounded bearer credential is required.", nameof(accessToken));

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(origin, path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budget.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized) return new(null, TenantEntryFailure.SignInRequired);
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                var failure = TenantEntryFailure.TenantDenied;
                if (string.Equals(response.Content.Headers.ContentType?.MediaType, "application/problem+json", StringComparison.OrdinalIgnoreCase))
                {
                    var bytes = await ReadBoundedAsync(response.Content, budget.Token).ConfigureAwait(false);
                    using var problem = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
                    if (problem.RootElement.ValueKind == JsonValueKind.Object && problem.RootElement.TryGetProperty("code", out var code) &&
                        code.ValueKind == JsonValueKind.String && code.GetString() is "account_not_bound" or "account_disabled")
                        failure = TenantEntryFailure.AccountUnavailable;
                }
                return new(null, failure);
            }
            if (response.StatusCode != HttpStatusCode.OK) return new(null, TenantEntryFailure.Unavailable);
            if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
                return new(null, TenantEntryFailure.InvalidResponse);
            var body = await ReadBoundedAsync(response.Content, budget.Token).ConfigureAwait(false);
            var value = JsonSerializer.Deserialize<T>(body, JsonOptions);
            return value is not null && validate(value) ? new(value, TenantEntryFailure.None) : new(null, TenantEntryFailure.InvalidResponse);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(null, TenantEntryFailure.TimedOut); }
        catch (HttpRequestException) { return new(null, TenantEntryFailure.Unavailable); }
        catch (JsonException) { return new(null, TenantEntryFailure.InvalidResponse); }
        catch (InvalidDataException) { return new(null, TenantEntryFailure.InvalidResponse); }
        catch (IOException) { return new(null, TenantEntryFailure.Unavailable); }
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
