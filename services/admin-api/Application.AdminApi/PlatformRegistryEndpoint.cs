using System.Text;
using Application.IdentityAccess;
using Application.Tenancy;
using Microsoft.AspNetCore.WebUtilities;

namespace Application.AdminApi;

internal static class PlatformRegistryEndpoint
{
    private const int DefaultLimit = 50;
    private const int MaximumLimit = 100;

    internal static async Task<IResult> BrowseTenantsAsync(
        HttpContext context, IPlatformTenantRegistry registry)
    {
        _ = AdminApiPlatformAuthorization.GetRequiredAccess(context);
        if (!TryPage(context, RegistryCursorKind.Tenant, out var limit, out var after, out var failure)) return failure!;
        var page = await registry.BrowseAsync(after, limit, context.RequestAborted).ConfigureAwait(false);
        return TypedResults.Ok(new RegistryPage<TenantRegistryResponse>(
            page.Items.Select(TenantResponse).ToArray(),
            RegistryCursor.Encode(RegistryCursorKind.Tenant, page.NextTenantId)));
    }

    internal static async Task<IResult> GetTenantAsync(
        Guid tenantId, HttpContext context, IPlatformTenantRegistry registry)
    {
        _ = AdminApiPlatformAuthorization.GetRequiredAccess(context);
        var item = await registry.FindAsync(tenantId, context.RequestAborted).ConfigureAwait(false);
        return item is null ? NotFound() : TypedResults.Ok(TenantResponse(item));
    }

    internal static async Task<IResult> BrowseAccountsAsync(
        HttpContext context, IPlatformAccountRegistry registry)
    {
        _ = AdminApiPlatformAuthorization.GetRequiredAccess(context);
        if (!TryPage(context, RegistryCursorKind.Account, out var limit, out var after, out var failure)) return failure!;
        var page = await registry.BrowseAsync(after, limit, context.RequestAborted).ConfigureAwait(false);
        return TypedResults.Ok(new RegistryPage<AccountRegistryResponse>(
            page.Items.Select(AccountResponse).ToArray(),
            RegistryCursor.Encode(RegistryCursorKind.Account, page.NextAccountId)));
    }

    internal static async Task<IResult> GetAccountAsync(
        Guid accountId, HttpContext context, IPlatformAccountRegistry registry)
    {
        _ = AdminApiPlatformAuthorization.GetRequiredAccess(context);
        var item = await registry.FindAsync(accountId, context.RequestAborted).ConfigureAwait(false);
        return item is null ? NotFound() : TypedResults.Ok(AccountResponse(item));
    }

    internal static async Task<IResult> BrowseMembershipsAsync(
        Guid tenantId, HttpContext context, IPlatformMembershipRegistry registry)
    {
        _ = AdminApiPlatformAuthorization.GetRequiredAccess(context);
        if (!TryPage(context, RegistryCursorKind.Membership, out var limit, out var after, out var failure)) return failure!;
        var page = await registry.BrowseAsync(tenantId, after, limit, context.RequestAborted).ConfigureAwait(false);
        return TypedResults.Ok(new RegistryPage<MembershipRegistryResponse>(
            page.Items.Select(MembershipResponse).ToArray(),
            RegistryCursor.Encode(RegistryCursorKind.Membership, page.NextAccountId)));
    }

    internal static async Task<IResult> GetMembershipAsync(
        Guid tenantId, Guid accountId, HttpContext context, IPlatformMembershipRegistry registry)
    {
        _ = AdminApiPlatformAuthorization.GetRequiredAccess(context);
        var item = await registry.FindAsync(tenantId, accountId, context.RequestAborted).ConfigureAwait(false);
        return item is null ? NotFound() : TypedResults.Ok(MembershipResponse(item));
    }

    private static bool TryPage(HttpContext context, RegistryCursorKind kind, out int limit, out Guid? after, out IResult? failure)
    {
        limit = DefaultLimit; after = null; failure = null;
        var limitValues = context.Request.Query["limit"];
        if (limitValues.Count > 1 || (limitValues.Count == 1 && (!int.TryParse(limitValues[0], out limit) || limit is < 1 or > MaximumLimit)))
        {
            failure = BoundedAdminJson.Problem(400, "invalid_registry_page", $"Registry limit must be from 1 through {MaximumLimit}.");
            return false;
        }
        var cursorValues = context.Request.Query["cursor"];
        if (cursorValues.Count > 1 || (cursorValues.Count == 1 && !RegistryCursor.TryDecode(kind, cursorValues[0], out after)))
        {
            failure = BoundedAdminJson.Problem(400, "invalid_registry_cursor", "The registry cursor is invalid for this resource.");
            return false;
        }
        return true;
    }

    private static IResult NotFound() => BoundedAdminJson.Problem(404, "registry_entry_not_found", "The registry entry was not found.");

    private static TenantRegistryResponse TenantResponse(PlatformTenantRegistryEntry item) => new(
        item.TenantId, item.DisplayName, item.Availability.ToString().ToLowerInvariant(), item.Revision,
        item.CreatedAt, item.SuspendedAt);

    private static AccountRegistryResponse AccountResponse(PlatformAccountRegistryEntry item) => new(
        item.AccountId, item.Availability.ToString().ToLowerInvariant(), item.CreatedAt, item.DisabledAt);

    private static MembershipRegistryResponse MembershipResponse(PlatformMembershipRegistryEntry item) => new(
        item.TenantId, item.AccountId, item.Availability.ToString().ToLowerInvariant(), item.Revision,
        item.InvitedAt, item.ActivatedAt, item.SuspendedAt, item.RemovedAt, item.IsInitialOwner);

}

internal sealed record RegistryPage<T>(IReadOnlyList<T> Items, string? NextCursor);
internal sealed record TenantRegistryResponse(Guid TenantId, string DisplayName, string Availability, int Revision, DateTimeOffset CreatedAt, DateTimeOffset? SuspendedAt);
internal sealed record AccountRegistryResponse(Guid AccountId, string Availability, DateTimeOffset CreatedAt, DateTimeOffset? DisabledAt);
internal sealed record MembershipRegistryResponse(Guid TenantId, Guid AccountId, string Availability, int Revision, DateTimeOffset InvitedAt, DateTimeOffset? ActivatedAt, DateTimeOffset? SuspendedAt, DateTimeOffset? RemovedAt, bool IsInitialOwner);

internal enum RegistryCursorKind { Tenant, Account, Membership }

internal static class RegistryCursor
{
    internal static string? Encode(RegistryCursorKind kind, Guid? id)
    {
        if (id is null) return null;
        var payload = $"{kind.ToString().ToLowerInvariant()}:v1:{id.Value:N}";
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
    }

    internal static bool TryDecode(RegistryCursorKind expected, string? value, out Guid? id)
    {
        id = null;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 160) return false;
        try
        {
            var payload = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value));
            var parts = payload.Split(':');
            if (parts.Length != 3 ||
                !string.Equals(parts[0], expected.ToString().ToLowerInvariant(), StringComparison.Ordinal) ||
                !string.Equals(parts[1], "v1", StringComparison.Ordinal) ||
                !Guid.TryParseExact(parts[2], "N", out var parsed) || parsed == Guid.Empty) return false;
            id = parsed;
            return true;
        }
        catch (FormatException) { return false; }
    }
}
