using System.Globalization;
using Application.Customers;
using Application.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Application.Customers.Postgres;

public sealed partial class PostgresCustomerStore
{
    public async Task<CustomerIndividualSnapshot?> ResolveCurrentCustomerAsync(TenantContext context, Guid customerId, CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty) throw new CustomerValidationException("customer_id_invalid", "Customer identity is required.");
        await using var session = await CustomerTenantDbSession.OpenAsync(dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        // Read original/survivor from one statement snapshot, never across a forward
        // consolidation commit between two READ COMMITTED queries.
        await using var command = session.CreateCommand(CustomerSql.FindCurrentCustomer);
        command.Parameters.AddWithValue("tenant_id", context.TenantId);
        command.Parameters.AddWithValue("id", customerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        var canonical = ReadIndividual(reader, context.TenantId);
        if (canonical.RedirectTargetIndividualId.HasValue)
            throw new InvalidOperationException("Customer redirect invariant is violated.");
        return canonical;
    }

    public async Task<CustomerDuplicateSearchResult> FindPotentialDuplicatesAsync(
        TenantContext context,
        FindCustomerDuplicatesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        RequireBoundedPageSize(request.Limit);
        request = request with
        {
            Signals = CustomerDuplicateSignals.Create(request.Signals.NormalizedName,
            request.Signals.NormalizedEmail, request.Signals.NormalizedPhone, request.Signals.NormalizedExternalRegistrationId,
            request.Signals.OrganizationId, request.Signals.ProgramId)
        };
        await using var session = await CustomerTenantDbSession.OpenAsync(
            dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        await using var command = session.CreateCommand(CustomerSql.FindPotentialDuplicates);
        command.Parameters.AddWithValue("tenant_id", context.TenantId);
        AddNullableUuid(command, "exclude_id", request.ExcludeCustomerId);
        AddNullableText(command, "email", request.Signals.NormalizedEmail);
        AddNullableText(command, "phone", request.Signals.NormalizedPhone);
        AddNullableText(command, "external_id", request.Signals.NormalizedExternalRegistrationId);
        AddNullableText(command, "name", request.Signals.NormalizedName);
        AddNullableUuid(command, "organization_id", request.Signals.OrganizationId);
        AddNullableUuid(command, "program_id", request.Signals.ProgramId);
        command.Parameters.AddWithValue("include_fuzzy", request.IncludeFuzzyNameDiscovery);
        command.Parameters.AddWithValue("limit", request.Limit + 1);

        var candidates = new List<CustomerDuplicateCandidate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var customer = ReadDuplicateCustomer(reader, context.TenantId);
            var evidence = new List<CustomerDuplicateEvidence>();
            if (reader.GetBoolean(reader.GetOrdinal("email_match")))
                evidence.Add(new(CustomerDuplicateEvidenceKind.NormalizedEmail));
            if (reader.GetBoolean(reader.GetOrdinal("phone_match")))
                evidence.Add(new(CustomerDuplicateEvidenceKind.NormalizedPhone));
            if (reader.GetBoolean(reader.GetOrdinal("external_match")))
                evidence.Add(new(CustomerDuplicateEvidenceKind.ExternalRegistrationId));
            if (reader.GetBoolean(reader.GetOrdinal("name_match")))
                evidence.Add(new(CustomerDuplicateEvidenceKind.ExactName));
            var candidateName = reader.IsDBNull(reader.GetOrdinal("candidate_normalized_name"))
                ? null : reader.GetString(reader.GetOrdinal("candidate_normalized_name"));
            if (request.IncludeFuzzyNameDiscovery && candidateName is not null
                && !evidence.Any(item => item.Kind is CustomerDuplicateEvidenceKind.NormalizedEmail
                    or CustomerDuplicateEvidenceKind.NormalizedPhone
                    or CustomerDuplicateEvidenceKind.ExternalRegistrationId
                    or CustomerDuplicateEvidenceKind.ExactName)
                && CustomerDuplicateMatcher.IsFuzzyNameDiscovery(request.Signals.NormalizedName, candidateName))
                evidence.Add(new(CustomerDuplicateEvidenceKind.FuzzyNameDiscovery));
            if (reader.GetBoolean(reader.GetOrdinal("organization_match")))
                evidence.Add(new(CustomerDuplicateEvidenceKind.OrganizationRelationship));
            if (reader.GetBoolean(reader.GetOrdinal("program_match")))
                evidence.Add(new(CustomerDuplicateEvidenceKind.ProgramRelationship));
            if (evidence.Count == 0) continue;
            var caseId = DuplicateCaseId(context.TenantId, request.ExcludeCustomerId ?? customer.IndividualId,
                customer.IndividualId);
            CustomerDuplicateResolutionSnapshot? resolution = reader.IsDBNull(reader.GetOrdinal("resolution_id")) ? null
                : new(reader.GetGuid(reader.GetOrdinal("resolution_id")), reader.GetGuid(reader.GetOrdinal("resolved_customer_id")),
                    reader.GetGuid(reader.GetOrdinal("resolved_other_customer_id")), (CustomerDuplicateOutcome)reader.GetInt32(reader.GetOrdinal("resolved_outcome")),
                    reader.GetGuid(reader.GetOrdinal("resolved_account_id")), reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("resolved_at")),
                    ReadOptional(reader, reader.GetOrdinal("resolved_reason")), reader.GetString(reader.GetOrdinal("resolved_evidence")));
            candidates.Add(new(caseId, customer, evidence,
                resolution is null || resolution.Outcome == CustomerDuplicateOutcome.PotentialDuplicate, resolution));
        }
        return new(candidates.Take(request.Limit).ToArray(), candidates.Count > request.Limit);
    }

    public async Task<ResolveCustomerDuplicateResult> ResolveDuplicateAsync(
        TenantContext context,
        ResolveCustomerDuplicateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        request = request with { Reason = CustomerIdentityNormalization.Reason(request.Reason) };
        if (request.Outcome is not (CustomerDuplicateOutcome.PotentialDuplicate or CustomerDuplicateOutcome.KeepSeparate or CustomerDuplicateOutcome.Dismiss))
            throw new CustomerValidationException("duplicate_outcome_invalid", "Resolution cannot consolidate.");
        var fingerprint = CustomerIdentityNormalization.Fingerprint("duplicate-resolve",
            request.CustomerId.ToString("N"), request.OtherCustomerId.ToString("N"),
            request.ExpectedCustomerRevision.ToString(CultureInfo.InvariantCulture),
            request.ExpectedOtherRevision.ToString(CultureInfo.InvariantCulture),
            ((int)request.Outcome).ToString(CultureInfo.InvariantCulture), request.Reason ?? "");
        await using var session = await CustomerTenantDbSession.OpenAsync(
            dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        var receipt = await FindDuplicateReceiptAsync(session, context, "resolve", idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is not null) return ReplayResolution(receipt.Value, fingerprint);

        await LockCustomerCanonicalizationAsync(session, context.TenantId, cancellationToken).ConfigureAwait(false);
        var pair = OrderedPair(request.CustomerId, request.OtherCustomerId);
        await using (var lockRows = session.CreateCommand(CustomerSql.LockDuplicateCustomers))
        {
            lockRows.Parameters.AddWithValue("tenant_id", context.TenantId);
            lockRows.Parameters.AddWithValue("first_id", pair.First);
            lockRows.Parameters.AddWithValue("second_id", pair.Second);
            await lockRows.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        receipt = await FindDuplicateReceiptAsync(session, context, "resolve", idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null) return ReplayResolution(receipt.Value, fingerprint);
        var first = await FindIndividualAsync(session, context.TenantId, pair.First, cancellationToken).ConfigureAwait(false);
        var second = await FindIndividualAsync(session, context.TenantId, pair.Second, cancellationToken).ConfigureAwait(false);
        if (first is null || second is null) return new(ResolveCustomerDuplicateStatus.NotFound, null);
        if (first.Revision != (request.CustomerId == pair.First ? request.ExpectedCustomerRevision : request.ExpectedOtherRevision)
            || second.Revision != (request.CustomerId == pair.Second ? request.ExpectedCustomerRevision : request.ExpectedOtherRevision))
            return new(ResolveCustomerDuplicateStatus.RevisionConflict, null);

        var caseId = DuplicateCaseId(context.TenantId, pair.First, pair.Second);
        var evidence = await ReadPairDuplicateEvidenceAsync(session, context.TenantId, pair, cancellationToken).ConfigureAwait(false);
        await using (var upsert = session.CreateCommand(CustomerSql.UpsertDuplicateCase))
        {
            upsert.Parameters.AddWithValue("tenant_id", context.TenantId);
            upsert.Parameters.AddWithValue("case_id", caseId);
            upsert.Parameters.AddWithValue("customer_id", pair.First);
            upsert.Parameters.AddWithValue("other_customer_id", pair.Second);
            upsert.Parameters.AddWithValue("evidence", evidence);
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        var resolutionId = Guid.CreateVersion7();
        var resolvedAt = CurrentStorageTime();
        await using (var update = session.CreateCommand(CustomerSql.UpdateDuplicateCase))
        {
            update.Parameters.AddWithValue("tenant_id", context.TenantId);
            update.Parameters.AddWithValue("case_id", caseId);
            update.Parameters.AddWithValue("outcome", (int)request.Outcome);
            update.Parameters.AddWithValue("account_id", context.AccountId);
            update.Parameters.AddWithValue("resolved_at", resolvedAt);
            AddNullableText(update, "reason", request.Reason);
            if (await update.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
            {
                await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new(ResolveCustomerDuplicateStatus.AlreadyResolved, null);
            }
        }

        var snapshot = new CustomerDuplicateResolutionSnapshot(resolutionId, request.CustomerId,
            request.OtherCustomerId, request.Outcome, context.AccountId, resolvedAt, request.Reason, evidence);
        if (await InsertDuplicateReceiptAsync(session, context, "resolve", idempotencyKey, fingerprint, snapshot,
                cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(ResolveCustomerDuplicateStatus.Resolved, snapshot);
        }
        receipt = await FindDuplicateReceiptAsync(session, context, "resolve", idempotencyKey, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Duplicate resolution receipt disappeared after conflict.");
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return ReplayResolution(receipt.Value, fingerprint);
    }

    public async Task<ConsolidateCustomerDuplicateResult> ConsolidateDuplicateAsync(
        TenantContext context,
        ConsolidateCustomerDuplicateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        request = request with { Reason = CustomerIdentityNormalization.Reason(request.Reason) };
        var fingerprint = CustomerIdentityNormalization.Fingerprint("duplicate-consolidate",
            request.SourceCustomerId.ToString("N"), request.CanonicalCustomerId.ToString("N"),
            request.ExpectedSourceRevision.ToString(CultureInfo.InvariantCulture),
            request.ExpectedCanonicalRevision.ToString(CultureInfo.InvariantCulture), request.Reason ?? "");
        await using var session = await CustomerTenantDbSession.OpenAsync(
            dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        var receipt = await FindDuplicateReceiptAsync(session, context, "consolidate", idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is not null) return ReplayConsolidation(receipt.Value, fingerprint);

        await LockCustomerCanonicalizationAsync(session, context.TenantId, cancellationToken).ConfigureAwait(false);
        // Receipt intent/result is historical. Check it again after serialization,
        // before interpreting today's successor graph or caller revisions.
        receipt = await FindDuplicateReceiptAsync(session, context, "consolidate", idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null) return ReplayConsolidation(receipt.Value, fingerprint);
        var pair = OrderedPair(request.SourceCustomerId, request.CanonicalCustomerId);
        var locked = new List<CustomerIndividualSnapshot>();
        await using (var lockRows = session.CreateCommand(CustomerSql.LockCustomerConsolidation))
        {
            lockRows.Parameters.AddWithValue("tenant_id", context.TenantId);
            lockRows.Parameters.AddWithValue("source_id", request.SourceCustomerId);
            lockRows.Parameters.AddWithValue("canonical_id", request.CanonicalCustomerId);
            await using var reader = await lockRows.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.GetGuid(0);
                // Incoming sources are locked by the ordered query, not materialized
                // into an unbounded application snapshot collection.
                if (id != request.SourceCustomerId && id != request.CanonicalCustomerId) continue;
                locked.Add(new(id, context.TenantId, string.Empty, null, null,
                    (CustomerIndividualAvailability)reader.GetInt32(3), reader.GetInt64(1), Guid.Empty, default, null, null, null, null,
                    "individual", null, null, null, null, null, reader.IsDBNull(2) ? null : reader.GetGuid(2)));
            }
        }
        var source = locked.SingleOrDefault(customer => customer.IndividualId == request.SourceCustomerId);
        var canonical = locked.SingleOrDefault(customer => customer.IndividualId == request.CanonicalCustomerId);
        if (source is null || canonical is null) return new(ConsolidateCustomerDuplicateStatus.NotFound, null, null);
        if (canonical.RedirectTargetIndividualId is Guid actualCanonicalId)
        {
            if (actualCanonicalId == source.IndividualId)
                return new(ConsolidateCustomerDuplicateStatus.CycleDetected, null, actualCanonicalId);
            // The supplied revision belongs to the physical requested target, not
            // its survivor. Do not guess authority over the survivor's revision.
            return new(ConsolidateCustomerDuplicateStatus.RevisionConflict, null, actualCanonicalId);
        }
        if (source.RedirectTargetIndividualId.HasValue)
            return new(ConsolidateCustomerDuplicateStatus.AlreadyRedirected, null, source.RedirectTargetIndividualId);
        if (source.Revision != request.ExpectedSourceRevision || canonical.Revision != request.ExpectedCanonicalRevision)
            return new(ConsolidateCustomerDuplicateStatus.RevisionConflict, null, null);
        if (canonical.Availability != CustomerIndividualAvailability.Active)
            return new(ConsolidateCustomerDuplicateStatus.InvalidTarget, null, null);
        var createdAt = CurrentStorageTime();
        var evidence = await ReadPairDuplicateEvidenceAsync(session, context.TenantId, pair, cancellationToken).ConfigureAwait(false);
        await using (var insertRedirect = session.CreateCommand(CustomerSql.InsertCustomerRedirect))
        {
            insertRedirect.Parameters.AddWithValue("tenant_id", context.TenantId);
            insertRedirect.Parameters.AddWithValue("source_id", request.SourceCustomerId);
            insertRedirect.Parameters.AddWithValue("canonical_id", request.CanonicalCustomerId);
            insertRedirect.Parameters.AddWithValue("account_id", context.AccountId);
            insertRedirect.Parameters.AddWithValue("created_at", createdAt);
            if (await insertRedirect.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
                return new(ConsolidateCustomerDuplicateStatus.AlreadyRedirected, null, request.CanonicalCustomerId);
        }
        await using (var redirectReferences = session.CreateCommand(CustomerSql.RedirectRepresentatives))
        {
            redirectReferences.Parameters.AddWithValue("tenant_id", context.TenantId);
            redirectReferences.Parameters.AddWithValue("source_id", request.SourceCustomerId);
            redirectReferences.Parameters.AddWithValue("canonical_id", request.CanonicalCustomerId);
            redirectReferences.Parameters.AddWithValue("account_id", context.AccountId);
            redirectReferences.Parameters.AddWithValue("changed_at", createdAt);
            await redirectReferences.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await using (var updateSource = session.CreateCommand(CustomerSql.UpdateCustomerRedirect))
        {
            updateSource.Parameters.AddWithValue("tenant_id", context.TenantId);
            updateSource.Parameters.AddWithValue("source_id", request.SourceCustomerId);
            updateSource.Parameters.AddWithValue("canonical_id", request.CanonicalCustomerId);
            await updateSource.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        var caseId = DuplicateCaseId(context.TenantId, pair.First, pair.Second);
        await using (var upsert = session.CreateCommand(CustomerSql.UpsertDuplicateCase))
        {
            upsert.Parameters.AddWithValue("tenant_id", context.TenantId);
            upsert.Parameters.AddWithValue("case_id", caseId);
            upsert.Parameters.AddWithValue("customer_id", pair.First);
            upsert.Parameters.AddWithValue("other_customer_id", pair.Second);
            upsert.Parameters.AddWithValue("evidence", evidence);
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await using (var update = session.CreateCommand(CustomerSql.UpdateDuplicateCase))
        {
            update.Parameters.AddWithValue("tenant_id", context.TenantId);
            update.Parameters.AddWithValue("case_id", caseId);
            update.Parameters.AddWithValue("account_id", context.AccountId);
            update.Parameters.AddWithValue("outcome", 3);
            update.Parameters.AddWithValue("resolved_at", createdAt);
            AddNullableText(update, "reason", request.Reason);
            if (await update.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
                return new(ConsolidateCustomerDuplicateStatus.InvalidTarget, null, null);
        }
        var snapshot = new CustomerDuplicateResolutionSnapshot(Guid.CreateVersion7(), request.SourceCustomerId,
            request.CanonicalCustomerId, CustomerDuplicateOutcome.ConsolidateInto, context.AccountId, createdAt, request.Reason, evidence);
        if (await InsertDuplicateReceiptAsync(session, context, "consolidate", idempotencyKey, fingerprint, snapshot,
                cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(ConsolidateCustomerDuplicateStatus.Consolidated, snapshot, request.CanonicalCustomerId);
        }
        receipt = await FindDuplicateReceiptAsync(session, context, "consolidate", idempotencyKey, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Duplicate consolidation receipt disappeared after conflict.");
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return ReplayConsolidation(receipt.Value, fingerprint);
    }

    private static CustomerIndividualSnapshot ReadDuplicateCustomer(NpgsqlDataReader reader, Guid tenantId)
    {
        int Ordinal(string name) => reader.GetOrdinal(name);
        var email = Ordinal("email"); var phone = Ordinal("phone");
        var availabilityChangedBy = Ordinal("availability_changed_by_account_id");
        var availabilityChangedAt = Ordinal("availability_changed_at");
        var contactChangedBy = Ordinal("contact_changed_by_account_id");
        var contactChangedAt = Ordinal("contact_changed_at");
        return new(reader.GetGuid(Ordinal("id")), tenantId, reader.GetString(Ordinal("display_name")),
            reader.IsDBNull(email) ? null : reader.GetString(email), reader.IsDBNull(phone) ? null : reader.GetString(phone),
            (CustomerIndividualAvailability)reader.GetInt32(Ordinal("availability")), reader.GetInt64(Ordinal("revision")),
            reader.GetGuid(Ordinal("created_by_account_id")), reader.GetFieldValue<DateTimeOffset>(Ordinal("created_at")),
            reader.IsDBNull(availabilityChangedBy) ? null : reader.GetGuid(availabilityChangedBy),
            reader.IsDBNull(availabilityChangedAt) ? null : reader.GetFieldValue<DateTimeOffset>(availabilityChangedAt),
            reader.IsDBNull(contactChangedBy) ? null : reader.GetGuid(contactChangedBy),
            reader.IsDBNull(contactChangedAt) ? null : reader.GetFieldValue<DateTimeOffset>(contactChangedAt),
            reader.IsDBNull(Ordinal("customer_type")) ? "individual" : reader.GetString(Ordinal("customer_type")),
            reader.IsDBNull(Ordinal("external_registration_id")) ? null : reader.GetString(Ordinal("external_registration_id")),
            reader.IsDBNull(Ordinal("address_line1")) ? null : reader.GetString(Ordinal("address_line1")),
            reader.IsDBNull(Ordinal("address_line2")) ? null : reader.GetString(Ordinal("address_line2")),
            reader.IsDBNull(Ordinal("city")) ? null : reader.GetString(Ordinal("city")),
            reader.IsDBNull(Ordinal("notes")) ? null : reader.GetString(Ordinal("notes")),
            reader.IsDBNull(Ordinal("redirect_target_individual_id")) ? null : reader.GetGuid(Ordinal("redirect_target_individual_id")));
    }

    public async Task<IReadOnlyList<CustomerDuplicateResolutionSnapshot>> ReadResolutionsAsync(
        TenantContext context, Guid customerId, Guid? afterResolutionId, int limit, CancellationToken cancellationToken)
    {
        RequireBoundedPageSize(limit);
        await using var session = await CustomerTenantDbSession.OpenAsync(dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        await using var command = session.CreateCommand(CustomerSql.ReadDuplicateResolutions);
        command.Parameters.AddWithValue("tenant_id", context.TenantId);
        command.Parameters.AddWithValue("customer_id", customerId);
        AddNullableUuid(command, "after_id", afterResolutionId);
        command.Parameters.AddWithValue("limit", limit);
        var results = new List<CustomerDuplicateResolutionSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            results.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), (CustomerDuplicateOutcome)reader.GetInt32(3),
                reader.GetGuid(4), reader.GetFieldValue<DateTimeOffset>(5), ReadOptional(reader, 6), reader.GetString(7)));
        return results;
    }

    private static Guid DuplicateCaseId(Guid tenantId, Guid left, Guid right)
    {
        var pair = OrderedPair(left, right);
        return CustomerIdentityNormalization.StableGuid(tenantId, pair.First, pair.Second);
    }

    private static (Guid First, Guid Second) OrderedPair(Guid left, Guid right) =>
        left.CompareTo(right) < 0 ? (left, right) : (right, left);

    private static async Task LockCustomerCanonicalizationAsync(CustomerTenantDbSession session, Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(CustomerSql.LockCustomerCanonicalization);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ReadPairDuplicateEvidenceAsync(CustomerTenantDbSession session, Guid tenantId,
        (Guid First, Guid Second) pair, CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(CustomerSql.ReadPairDuplicateEvidence);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("first_id", pair.First);
        command.Parameters.AddWithValue("second_id", pair.Second);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) throw new InvalidOperationException("Locked duplicate pair disappeared.");
        CustomerDuplicateEvidenceKind[] kinds = [CustomerDuplicateEvidenceKind.NormalizedEmail, CustomerDuplicateEvidenceKind.NormalizedPhone,
            CustomerDuplicateEvidenceKind.ExternalRegistrationId, CustomerDuplicateEvidenceKind.ExactName,
            CustomerDuplicateEvidenceKind.OrganizationRelationship, CustomerDuplicateEvidenceKind.ProgramRelationship];
        return string.Join(',', kinds.Where((_, index) => !reader.IsDBNull(index) && reader.GetBoolean(index)));
    }

    private static async Task<(string Fingerprint, CustomerDuplicateResolutionSnapshot Snapshot)?> FindDuplicateReceiptAsync(
        CustomerTenantDbSession session, TenantContext context, string operation, string key, CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(CustomerSql.FindDuplicateReceipt);
        command.Parameters.AddWithValue("tenant_id", context.TenantId);
        command.Parameters.AddWithValue("account_id", context.AccountId);
        command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        var reason = reader.IsDBNull(6) ? null : reader.GetString(6);
        return (reader.GetString(0), new(reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3),
            (CustomerDuplicateOutcome)reader.GetInt32(4), context.AccountId,
            reader.GetFieldValue<DateTimeOffset>(5), reason, reader.GetString(7)));
    }

    private static async Task<bool> InsertDuplicateReceiptAsync(
        CustomerTenantDbSession session, TenantContext context, string operation, string key, string fingerprint,
        CustomerDuplicateResolutionSnapshot snapshot, CancellationToken cancellationToken)
    {
        await using var insert = session.CreateCommand(CustomerSql.InsertDuplicateReceipt);
        insert.Parameters.AddWithValue("tenant_id", context.TenantId);
        insert.Parameters.AddWithValue("account_id", context.AccountId);
        insert.Parameters.AddWithValue("operation", operation);
        insert.Parameters.AddWithValue("key", key);
        insert.Parameters.AddWithValue("fingerprint", fingerprint);
        insert.Parameters.AddWithValue("resolution_id", snapshot.ResolutionId);
        insert.Parameters.AddWithValue("customer_id", snapshot.CustomerId);
        insert.Parameters.AddWithValue("other_customer_id", snapshot.OtherCustomerId);
        insert.Parameters.AddWithValue("outcome", (int)snapshot.Outcome);
        insert.Parameters.AddWithValue("resolved_at", snapshot.ResolvedAt);
        AddNullableText(insert, "reason", snapshot.Reason);
        insert.Parameters.AddWithValue("evidence", snapshot.Evidence);
        return await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    private static ResolveCustomerDuplicateResult ReplayResolution(
        (string Fingerprint, CustomerDuplicateResolutionSnapshot Snapshot) receipt, string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? new(ResolveCustomerDuplicateStatus.Replayed, receipt.Snapshot)
            : new(ResolveCustomerDuplicateStatus.IdempotencyKeyConflict, null);

    private static ConsolidateCustomerDuplicateResult ReplayConsolidation(
        (string Fingerprint, CustomerDuplicateResolutionSnapshot Snapshot) receipt, string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? new(ConsolidateCustomerDuplicateStatus.Replayed, receipt.Snapshot, receipt.Snapshot.OtherCustomerId)
            : new(ConsolidateCustomerDuplicateStatus.IdempotencyKeyConflict, null, null);

    private static void AddNullableUuid(NpgsqlCommand command, string name, Guid? value) =>
        command.Parameters.Add(name, NpgsqlDbType.Uuid).Value = (object?)value ?? DBNull.Value;

    private static void AddNullableText(NpgsqlCommand command, string name, string? value) =>
        command.Parameters.Add(name, NpgsqlDbType.Varchar).Value = (object?)value ?? DBNull.Value;
}
