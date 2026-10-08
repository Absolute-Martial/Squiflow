using System.Text.Json;
using Application.Pricing;
using NpgsqlTypes;

namespace Application.Pricing.Postgres;

public sealed partial class PostgresPricingStore
{
    public async Task<PricingPolicySnapshot?> GetCurrentPolicyAsync(PricingActorContext actor, CancellationToken ct)
    {
        await using var session = await PricingTenantDbSession.OpenSnapshotAsync(dataSource, actor.TenantId, ct).ConfigureAwait(false);
        var result = await ReadPolicyAsync(session, actor.TenantId, ct).ConfigureAwait(false);
        await session.CommitAsync(ct).ConfigureAwait(false);
        return result;
    }

    public async Task<PublishPricingPolicyResult> PublishPolicyAsync(PricingActorContext actor, PublishPricingPolicyRequest request, string key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireIdempotencyKey(key);
        if (request.ExpectedRevision is < 0 or long.MaxValue || request.MinimumUnitPrice < 0 || request.MaximumUnitPrice > 999999999999999.9999m ||
            decimal.Round(request.MinimumUnitPrice, 4) != request.MinimumUnitPrice || decimal.Round(request.MaximumUnitPrice, 4) != request.MaximumUnitPrice ||
            (request.MaximumDecreasePercent.HasValue && decimal.Round(request.MaximumDecreasePercent.Value, 4) != request.MaximumDecreasePercent) ||
            (request.MaximumIncreasePercent.HasValue && decimal.Round(request.MaximumIncreasePercent.Value, 4) != request.MaximumIncreasePercent))
            throw new PricingValidationException("pricing_policy_invalid", "Policy bounds must fit supported decimal precision.");
        var policy = new PricingOverridePolicy(checked(request.ExpectedRevision + 1), request.MinimumUnitPrice,
            request.MaximumUnitPrice, request.MaximumDecreasePercent, request.MaximumIncreasePercent);
        await using var session = await PricingTenantDbSession.OpenPublicationAsync(dataSource, actor.TenantId, ct).ConfigureAwait(false);
        await LockAsync(session, $"policy|{actor.TenantId:D}", ct).ConfigureAwait(false);
        var receipt = await FindReceiptAsync(session, actor, "publish-policy", key, ct).ConfigureAwait(false);
        if (receipt.HasValue)
        {
            var replay = receipt.Value.Fingerprint != request.Fingerprint
                ? new PublishPricingPolicyResult(PublishPricingPolicyStatus.IdempotencyKeyConflict, null)
                : new(PublishPricingPolicyStatus.Replayed,
                    JsonSerializer.Deserialize<PricingPolicySnapshot>(receipt.Value.ResponseJson, ReceiptJsonOptions)
                    ?? throw new InvalidOperationException("Malformed retained pricing policy."));
            await session.CommitAsync(ct).ConfigureAwait(false);
            return replay;
        }
        var current = await ReadPolicyAsync(session, actor.TenantId, ct).ConfigureAwait(false);
        if ((current?.Policy.PolicyRevision ?? 0) != request.ExpectedRevision)
        {
            await session.CommitAsync(ct).ConfigureAwait(false);
            return new(PublishPricingPolicyStatus.RevisionConflict, null);
        }
        var now = GetDatabaseInstant();
        var snapshot = new PricingPolicySnapshot(actor.TenantId, policy, actor.AccountId, now);
        await using var insert = session.CreateCommand(PricingSql.InsertPolicy);
        insert.Parameters.AddWithValue("tenant_id", actor.TenantId);
        insert.Parameters.AddWithValue("revision", policy.PolicyRevision);
        insert.Parameters.AddWithValue("minimum", policy.MinimumUnitPrice);
        insert.Parameters.AddWithValue("maximum", policy.MaximumUnitPrice);
        insert.Parameters.Add("decrease", NpgsqlDbType.Numeric).Value = (object?)policy.MaximumDecreasePercent ?? DBNull.Value;
        insert.Parameters.Add("increase", NpgsqlDbType.Numeric).Value = (object?)policy.MaximumIncreasePercent ?? DBNull.Value;
        insert.Parameters.AddWithValue("account_id", actor.AccountId);
        insert.Parameters.AddWithValue("created_at", now);
        await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (!await TryInsertReceiptAsync(session, actor, "publish-policy", key, request.Fingerprint,
                JsonSerializer.Serialize(snapshot, ReceiptJsonOptions), now, ct).ConfigureAwait(false))
            throw new InvalidOperationException("Pricing policy receipt lock invariant failed.");
        await session.CommitAsync(ct).ConfigureAwait(false);
        return new(PublishPricingPolicyStatus.Published, snapshot);
    }

    private static async Task<PricingPolicySnapshot?> ReadPolicyAsync(PricingTenantDbSession session, Guid tenantId, CancellationToken ct)
    {
        await using var command = session.CreateCommand(PricingSql.FindPolicy);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        return new(tenantId, new PricingOverridePolicy(reader.GetInt64(0), reader.GetDecimal(1), reader.GetDecimal(2),
            reader.IsDBNull(3) ? null : reader.GetDecimal(3), reader.IsDBNull(4) ? null : reader.GetDecimal(4)),
            reader.GetGuid(5), reader.GetFieldValue<DateTimeOffset>(6));
    }
}
