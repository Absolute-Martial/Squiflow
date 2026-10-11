using Application.Tenancy;
using Npgsql;

namespace Application.Customers.Postgres;

public sealed record CustomerOrganizationBillingObservation(Guid TenantId, Guid OrganizationId, string DisplayName);
public sealed record CustomerProgramBillingObservation(Guid TenantId, Guid ProgramId, Guid OrganizationId, string DisplayName);
public sealed record CustomerIndividualBillingObservation(
    Guid TenantId, Guid IndividualId, string DisplayName, long Revision, CustomerIndividualAvailability Availability);

public static class CustomerBillingTransactionReader
{
    private static readonly string OrganizationSql = Load("ReadOrganizationBillingObservation");
    private static readonly string ProgramSql = Load("ReadProgramBillingObservation");
    private static readonly string IndividualSql = Load("ReadIndividualBillingObservation");

    public static async Task<CustomerOrganizationBillingObservation?> ReadOrganizationAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, TenantContext context, Guid organizationId, CancellationToken cancellationToken)
    {
        await PrepareAsync(connection, transaction, context, organizationId, cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(OrganizationSql, connection, transaction);
        command.Parameters.AddWithValue("tenant_id", context.TenantId);
        command.Parameters.AddWithValue("id", organizationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        var result = new CustomerOrganizationBillingObservation(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2));
        if (result.TenantId != context.TenantId || result.OrganizationId != organizationId ||
            CustomerOrganizationIntent.Create(new(result.DisplayName)).DisplayName != result.DisplayName ||
            await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Customers returned invalid organization billing facts.");
        return result;
    }

    public static async Task<CustomerProgramBillingObservation?> ReadProgramAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, TenantContext context, Guid programId, CancellationToken cancellationToken)
    {
        await PrepareAsync(connection, transaction, context, programId, cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(ProgramSql, connection, transaction);
        command.Parameters.AddWithValue("tenant_id", context.TenantId);
        command.Parameters.AddWithValue("id", programId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        var result = new CustomerProgramBillingObservation(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3));
        if (result.TenantId != context.TenantId || result.ProgramId != programId || result.OrganizationId == Guid.Empty ||
            CustomerProgramIntent.Create(new(result.OrganizationId, result.DisplayName)).DisplayName != result.DisplayName ||
            await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Customers returned invalid program billing facts.");
        return result;
    }

    public static async Task<CustomerIndividualBillingObservation?> ReadIndividualAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, TenantContext context, Guid individualId, CancellationToken cancellationToken)
    {
        await PrepareAsync(connection, transaction, context, individualId, cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(IndividualSql, connection, transaction);
        command.Parameters.AddWithValue("tenant_id", context.TenantId);
        command.Parameters.AddWithValue("id", individualId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        var availability = reader.GetInt32(4) switch
        {
            1 => CustomerIndividualAvailability.Active,
            2 => CustomerIndividualAvailability.Inactive,
            _ => throw new InvalidOperationException("Customers returned unsupported individual availability."),
        };
        var result = new CustomerIndividualBillingObservation(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2),
            reader.GetInt64(3), availability);
        if (result.TenantId != context.TenantId || result.IndividualId != individualId || result.Revision < 1 ||
            CustomerIndividualIntent.Create(new(result.DisplayName, null, null)).DisplayName != result.DisplayName ||
            await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Customers returned invalid individual billing facts.");
        return result;
    }

    private static async Task PrepareAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantContext context,
        Guid id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(context);
        if (connection.State != System.Data.ConnectionState.Open || !ReferenceEquals(transaction.Connection, connection) ||
            context.TenantId == Guid.Empty || context.AccountId == Guid.Empty || id == Guid.Empty)
            throw new ArgumentException("An active transaction and non-empty tenant, account and customer identities are required.");
        await using var command = new NpgsqlCommand(CustomerSql.SetTenant, connection, transaction);
        command.Parameters.AddWithValue("tenant_id", context.TenantId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string Load(string name)
    {
        using var stream = typeof(CustomerBillingTransactionReader).Assembly.GetManifestResourceStream(
            $"{typeof(CustomerBillingTransactionReader).Namespace}.Sql.{name}.sql")
            ?? throw new InvalidOperationException("Missing Customers billing observation SQL.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
