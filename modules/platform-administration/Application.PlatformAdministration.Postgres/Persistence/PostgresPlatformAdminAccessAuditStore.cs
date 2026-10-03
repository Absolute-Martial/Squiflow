using Npgsql;

namespace Application.PlatformAdministration.Postgres;

public sealed class PostgresPlatformAdminAccessAuditStore(NpgsqlDataSource dataSource)
    : IPlatformAdminAccessAuditStore
{
    public async Task AppendAsync(
        PlatformAdminAccessAuditEntry entry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using var command = dataSource.CreateCommand("""
            INSERT INTO platform_administration.access_audit_events
                (id, principal_id, device_id, issuer, subject, certificate_fingerprint,
                 operation, outcome, reason, occurred_at)
            VALUES
                ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
            """);
        command.Parameters.AddWithValue(entry.Id);
        command.Parameters.AddWithValue(entry.PrincipalId is { } principalId ? principalId : DBNull.Value);
        command.Parameters.AddWithValue(entry.DeviceId is { } deviceId ? deviceId : DBNull.Value);
        command.Parameters.AddWithValue(entry.Administrator.Issuer);
        command.Parameters.AddWithValue(entry.Administrator.Subject);
        command.Parameters.AddWithValue(
            entry.DeviceCertificateFingerprint is { } fingerprint ? fingerprint.Value : DBNull.Value);
        command.Parameters.AddWithValue(entry.Operation);
        command.Parameters.AddWithValue((short)entry.Outcome);
        command.Parameters.AddWithValue(entry.Reason);
        command.Parameters.AddWithValue(entry.OccurredAt);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
