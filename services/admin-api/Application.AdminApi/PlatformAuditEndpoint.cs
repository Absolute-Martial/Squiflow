using System.Globalization;
using System.Text;
using Application.IdentityAccess;
using Application.PlatformAdministration;
using Microsoft.AspNetCore.WebUtilities;

namespace Application.AdminApi;

internal static class PlatformAuditEndpoint
{
    internal static async Task<IResult> BrowseAccessAsync(HttpContext context, IPlatformAdminAuditReader reader)
    {
        _ = AdminApiPlatformAuthorization.GetRequiredAccess(context);
        if (!TryLimit(context, ["limit", "cursor", "principalId", "outcome"], out var limit, out var failure)) return failure!;
        Guid? principalId = null;
        var principal = context.Request.Query["principalId"];
        if (principal.Count == 1)
        {
            if (!Guid.TryParseExact(principal[0], "D", out var parsed) || parsed == Guid.Empty) return InvalidPage();
            principalId = parsed;
        }
        PlatformAdminAccessAuditOutcome? outcome = null;
        var outcomeValue = context.Request.Query["outcome"];
        if (outcomeValue.Count == 1)
        {
            outcome = outcomeValue[0] switch
            {
                "authorized" => PlatformAdminAccessAuditOutcome.Succeeded,
                "denied" => PlatformAdminAccessAuditOutcome.Denied,
                _ => null,
            };
            if (outcome is null) return InvalidPage();
        }
        PlatformAdminAuditPosition? before = null;
        var cursor = context.Request.Query["cursor"];
        if (cursor.Count == 1 && !TryAccessCursor(cursor[0], principalId, outcome, out before)) return InvalidCursor();
        var page = await reader.BrowseAccessAsync(PlatformAdminAccessAuditQuery.Create(limit, before, principalId, outcome),
            context.RequestAborted).ConfigureAwait(false);
        return TypedResults.Ok(new
        {
            kind = "access_decision",
            items = page.Items.Select(item => new
            {
                item.Id,
                item.PrincipalId,
                item.DeviceId,
                item.Operation,
                outcome = item.Outcome == PlatformAdminAccessAuditOutcome.Succeeded ? "authorized" : "denied",
                item.Reason,
                item.OccurredAt,
            }).ToArray(),
            nextCursor = page.NextPosition is { } next
                ? Encode($"access-audit:v1:{next.OccurredAt.UtcTicks.ToString(CultureInfo.InvariantCulture)}:{next.Id:N}:{principalId:N}:{(int?)outcome ?? 0}")
                : null,
        });
    }

    internal static async Task<IResult> BrowseAccountHistoryAsync(Guid accountId, HttpContext context,
        IAccountLifecycleHistory history, IPlatformAccountRegistry accounts)
    {
        _ = AdminApiPlatformAuthorization.GetRequiredAccess(context);
        if (accountId == Guid.Empty) return InvalidPage();
        if (!TryLimit(context, ["limit", "cursor"], out var limit, out var failure)) return failure!;
        int? before = null;
        var cursor = context.Request.Query["cursor"];
        if (cursor.Count == 1)
        {
            var parts = Decode(cursor[0]);
            if (parts is not ["account-history", "v1", var account, var revision] ||
                account != accountId.ToString("N") ||
                !int.TryParse(revision, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < 2)
                return InvalidCursor();
            before = parsed;
        }
        if (await accounts.FindAsync(accountId, context.RequestAborted).ConfigureAwait(false) is null)
            return BoundedAdminJson.Problem(404, "account_not_found", "The account was not found.");
        var page = await history.BrowseAsync(accountId, before, limit, context.RequestAborted).ConfigureAwait(false);
        return TypedResults.Ok(new
        {
            kind = "committed_account_transition",
            items = page.Items.Select(item => new
            {
                item.AccountId,
                item.Revision,
                availability = item.Availability.ToString().ToLowerInvariant(),
                item.DisabledAt,
                item.ChangedByPrincipalId,
                item.ChangedByDeviceId,
                item.Reason,
                item.ChangedAt,
            }).ToArray(),
            nextCursor = page.NextRevision is { } next ? Encode($"account-history:v1:{accountId:N}:{next.ToString(CultureInfo.InvariantCulture)}") : null,
        });
    }

    private static bool TryLimit(HttpContext context, string[] allowed, out int limit, out IResult? failure)
    {
        limit = 50;
        failure = null;
        if (context.Request.Query.Any(pair => !allowed.Contains(pair.Key, StringComparer.Ordinal) || pair.Value.Count != 1) ||
            (context.Request.Query.TryGetValue("limit", out var value) &&
             (!int.TryParse(value[0], NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > 100)))
        {
            failure = InvalidPage();
            return false;
        }
        return true;
    }

    private static bool TryAccessCursor(string? cursor, Guid? principal, PlatformAdminAccessAuditOutcome? outcome,
        out PlatformAdminAuditPosition? before)
    {
        before = null;
        var parts = Decode(cursor);
        if (parts is not ["access-audit", "v1", var timestamp, var id, var filterPrincipal, var filterOutcome] ||
            filterPrincipal != $"{principal:N}" || filterOutcome != ((int?)outcome ?? 0).ToString(CultureInfo.InvariantCulture) ||
            !Guid.TryParseExact(id, "N", out var parsedId) || parsedId == Guid.Empty ||
            !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ||
            ticks < DateTimeOffset.MinValue.Ticks || ticks > DateTimeOffset.MaxValue.Ticks || ticks % 10 != 0) return false;
        before = new(new DateTimeOffset(ticks, TimeSpan.Zero), parsedId);
        return true;
    }

    private static string Encode(string payload) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payload));

    private static string[]? Decode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256) return null;
        try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value)).Split(':'); }
        catch (FormatException) { return null; }
    }

    private static IResult InvalidPage() => BoundedAdminJson.Problem(400, "invalid_audit_page", "The audit filters or page size are invalid; limit must be from 1 through 100.");
    private static IResult InvalidCursor() => BoundedAdminJson.Problem(400, "invalid_audit_cursor", "The audit cursor is invalid for this resource and filters.");
}
