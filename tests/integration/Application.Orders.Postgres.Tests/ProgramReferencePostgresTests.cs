using Application.Customers;
using Application.Orders;
using Application.Orders.Postgres;
using Application.PlatformAdministration;
using Application.Profiles;
using Application.Profiles.Postgres;
using Application.Tenancy;
using Npgsql;
using Xunit;

namespace Application.Orders.Postgres.Tests;

public sealed partial class OrderMigrationAndRlsTests
{
    [Fact]
    public async Task ProgramOrderKeepsItsPublishedPolicyAndReferenceReplayBeforeExactPriceCommit()
    {
        await ApplyOrderSchemaAsync();
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        await SeedCustomerContextAsync(tenantId, accountId, organizationId, programId);

        await using var profileSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        var clock = new FixedTimeProvider();
        var profiles = new PostgresProfileStore(profileSource, clock);
        var context = await ResolveContextAsync(tenantId, accountId);
        var platformAccess = new PlatformAdminAccess(Guid.NewGuid(), Guid.NewGuid());
        var oldOptionalProfile = await PublishAndActivateProfileAsync(
            profiles, context, platformAccess, requireReference: false, legacyBaseline: false, "optional");

        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var runtimeSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var profileBridge = new ProfileOrderPolicySource();
        var orders = new PostgresOrderDraftStore(runtimeSource, clock, profilePolicySource: profileBridge);
        var customerContext = new CustomerOrderContext(organizationId, programId);
        var earlierDraft = (await orders.CreateAsync(
            context, CreateIntent("Earlier optional program order", customerContext), "earlier-create", default)).Order!;
        Assert.Equal(oldOptionalProfile.ProfileId, earlierDraft.ProgramPolicy?.ProfileId);
        Assert.False(earlierDraft.ProgramPolicy!.RequireReferenceForProgramOrders);

        var requiredProfile = await PublishAndActivateProfileAsync(
            profiles, context, platformAccess, requireReference: true, legacyBaseline: false, "required");
        var earlierCommit = await new CommitOrderDraft(orders).ExecuteAsync(
            context, new(earlierDraft.OrderId, earlierDraft.Revision), "earlier-commit", default);
        Assert.Equal(CommitOrderDraftStatus.Committed, earlierCommit.Status);
        Assert.Equal(oldOptionalProfile.ProfileId, earlierCommit.Order?.ProgramPolicy?.ProfileId);
        Assert.False(earlierCommit.Order!.ProgramPolicy!.RequireReferenceForProgramOrders);
        Assert.Null(earlierCommit.Order?.ExternalProgramReference);
        Assert.Equal(25m, earlierCommit.Order?.Total);

        var currentDraft = (await orders.CreateAsync(
            context, CreateIntent("Required program order", customerContext), "required-create", default)).Order!;
        Assert.Equal(requiredProfile.ProfileId, currentDraft.ProgramPolicy?.ProfileId);
        Assert.True(currentDraft.ProgramPolicy!.RequireReferenceForProgramOrders);
        Assert.Null(currentDraft.ExternalProgramReference);

        var commit = new CommitOrderDraft(orders);
        var requiredCommit = new CommitOrderDraftRequest(currentDraft.OrderId, currentDraft.Revision);
        var receiptsBeforeDeniedCommit = await CountReceiptsAsync(tenantId);
        Assert.Equal(CommitOrderDraftStatus.ProgramReferenceRequired,
            (await commit.ExecuteAsync(context, requiredCommit, "required-commit", default)).Status);
        Assert.Equivalent(currentDraft, await orders.FindAsync(context, currentDraft.OrderId, default));
        Assert.Equal(receiptsBeforeDeniedCommit, await CountReceiptsAsync(tenantId));

        var setReference = new SetOrderProgramReference(orders);
        var referenceRequest = new SetOrderProgramReferenceRequest(currentDraft.OrderId, currentDraft.Revision, " PO-204 ");
        var assigned = await setReference.ExecuteAsync(context, referenceRequest, "reference-key", default);
        Assert.Equal(SetOrderProgramReferenceStatus.Updated, assigned.Status);
        Assert.Equal(currentDraft.Revision + 1, assigned.Order?.Revision);
        Assert.Equal("PO-204", assigned.Order?.ExternalProgramReference);
        Assert.Equal(currentDraft.ProgramPolicy, assigned.Order?.ProgramPolicy);
        var receiptCountAfterAssignment = await CountReceiptsAsync(tenantId);
        var replay = await setReference.ExecuteAsync(context, referenceRequest, "reference-key", default);
        Assert.Equal(SetOrderProgramReferenceStatus.Replayed, replay.Status);
        Assert.Equivalent(assigned.Order, replay.Order);
        Assert.Equal(receiptCountAfterAssignment, await CountReceiptsAsync(tenantId));

        var committed = await commit.ExecuteAsync(
            context, requiredCommit with { ExpectedRevision = assigned.Order!.Revision }, "required-commit-after-reference", default);
        Assert.Equal(CommitOrderDraftStatus.Committed, committed.Status);
        Assert.Equal(25m, committed.Order?.Total);
        Assert.Equivalent(currentDraft.Lines, committed.Order?.Lines);
        Assert.Equal("PO-204", committed.Order?.ExternalProgramReference);
        Assert.Equal(requiredProfile.ProfileId, committed.Order?.ProgramPolicy?.ProfileId);
    }

    [Fact]
    public async Task LegacyDraftRequiresExplicitOptionalBaselineAssignmentBeforeItCanCommit()
    {
        await ApplyOrderSchemaAsync();
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        await SeedCustomerContextAsync(tenantId, accountId, organizationId, programId);

        await using var profileSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        var clock = new FixedTimeProvider();
        var profiles = new PostgresProfileStore(profileSource, clock);
        var context = await ResolveContextAsync(tenantId, accountId);
        var baselineProfile = await PublishAndActivateProfileAsync(
            profiles, context, new(Guid.NewGuid(), Guid.NewGuid()), requireReference: false, legacyBaseline: true, "baseline");
        Assert.True(baselineProfile.IsLegacyBaseline);
        Assert.False(baselineProfile.Policy.RequireReferenceForProgramOrders);

        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var runtimeSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var legacyOrders = new PostgresOrderDraftStore(runtimeSource, clock);
        var created = (await legacyOrders.CreateAsync(
            context,
            CreateIntent("Legacy order before profile binding", new CustomerOrderContext(organizationId, programId)),
            "legacy-create",
            default)).Order!;
        Assert.Null(created.ProgramPolicy);

        var currentOrders = new PostgresOrderDraftStore(
            runtimeSource, clock, profilePolicySource: new ProfileOrderPolicySource());
        var failClosed = await new CommitOrderDraft(currentOrders).ExecuteAsync(
            context, new(created.OrderId, created.Revision), "commit-before-baseline", default);
        Assert.Equal(CommitOrderDraftStatus.ProfileUnavailable, failClosed.Status);
        Assert.Equivalent(created, await currentOrders.FindAsync(context, created.OrderId, default));

        var adminPrincipalId = Guid.NewGuid();
        var adminDeviceId = Guid.NewGuid();
        var adminConnectionString = await CreateLegacyAssignmentRuntimeRoleAsync();
        await using var adminSource = new NpgsqlDataSourceBuilder(adminConnectionString).Build();
        var adminOrders = new PostgresOrderDraftStore(
            adminSource, clock, profilePolicySource: new ProfileOrderPolicySource());
        Assert.Equal(LegacyOrderPolicyAssignmentStatus.RevisionConflict,
            (await adminOrders.AssignLegacyPolicyAsync(
                tenantId, created.OrderId, created.Revision + 1, adminPrincipalId, adminDeviceId, "stale-baseline", default)).Status);
        var assignment = await adminOrders.AssignLegacyPolicyAsync(
            tenantId, created.OrderId, created.Revision, adminPrincipalId, adminDeviceId, "baseline-assignment", default);
        Assert.Equal(LegacyOrderPolicyAssignmentStatus.Assigned, assignment.Status);
        Assert.Equal(created.Revision, assignment.ObservedRevision);
        Assert.Equal(LegacyOrderPolicyAssignmentStatus.Replayed,
            (await adminOrders.AssignLegacyPolicyAsync(
                tenantId, created.OrderId, created.Revision, adminPrincipalId, adminDeviceId, "baseline-assignment", default)).Status);
        var bound = await currentOrders.FindAsync(context, created.OrderId, default);
        Assert.Equal(created.Revision, bound?.Revision);
        Assert.Equal(baselineProfile.ProfileId, bound?.ProgramPolicy?.ProfileId);
        Assert.False(bound!.ProgramPolicy!.RequireReferenceForProgramOrders);
        Assert.Null(bound?.ExternalProgramReference);

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var evidence = new NpgsqlCommand("""
            SELECT baseline_principal_id, baseline_device_id, baseline_key, baseline_fingerprint, baseline_observed_revision
            FROM orders.program_order_metadata WHERE tenant_id=@tenant AND order_id=@order
            """, connection);
        evidence.Parameters.AddWithValue("tenant", tenantId);
        evidence.Parameters.AddWithValue("order", created.OrderId);
        await using (var reader = await evidence.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal(adminPrincipalId, reader.GetGuid(0));
            Assert.Equal(adminDeviceId, reader.GetGuid(1));
            Assert.Equal("baseline-assignment", reader.GetString(2));
            Assert.Matches("^[0-9a-f]{64}$", reader.GetString(3));
            Assert.Equal(created.Revision, reader.GetInt64(4));
        }

        var committed = await new CommitOrderDraft(currentOrders).ExecuteAsync(
            context, new(created.OrderId, created.Revision), "legacy-commit", default);
        Assert.Equal(CommitOrderDraftStatus.Committed, committed.Status);
        Assert.Equal(created.Total, committed.Order?.Total);
        Assert.Equivalent(created.Lines, committed.Order?.Lines);
        Assert.Null(committed.Order?.ExternalProgramReference);
        Assert.Equal(baselineProfile.ProfileId, committed.Order?.ProgramPolicy?.ProfileId);
    }

    [Fact]
    public async Task QuotedProgramOrderKeepsAcceptedPriceWhenItsRequiredReferenceIsAdded()
    {
        // The accepted-facts reader below isolates this Order-owned persistence claim; it is not a full Quotation conversion test.
        await ApplyOrderSchemaAsync();
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        await SeedCustomerContextAsync(tenantId, accountId, organizationId, programId);

        await using var profileSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        var clock = new FixedTimeProvider();
        var profiles = new PostgresProfileStore(profileSource, clock);
        var context = await ResolveContextAsync(tenantId, accountId);
        var profile = await PublishAndActivateProfileAsync(
            profiles, context, new(Guid.NewGuid(), Guid.NewGuid()), requireReference: true, legacyBaseline: false, "quoted");
        var policyFacts = new OrderProgramPolicyFacts(profile.ProfileId, profile.Policy.PolicyRevisionId, true);

        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var runtimeSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var customerContext = new CustomerOrderContext(organizationId, programId);
        var accepted = new AcceptedQuotationOrder(
            new OrderQuotationOrigin(Guid.NewGuid(), Guid.NewGuid(), 41, 3),
            "Accepted priced offer",
            "USD",
            60m,
            [new OrderDraftLine(1, "Accepted printed panels", 2m, "EA", 30m, 60m)],
            customerContext);

        OrderDraftSnapshot quoted;
        await using (var connection = await runtimeSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            quoted = await PostgresOrderDraftStore.CreateAcceptedQuotationAsync(
                context, accepted, connection, transaction, clock.GetUtcNow(), default, policyFacts);
            await transaction.CommitAsync();
        }

        var acceptedReader = new FixedAcceptedQuotationReader(quoted.OrderId, accepted);
        var guard = new OrderCommercialCommitGuard(null!, null!, null!, acceptedReader);
        var orders = new PostgresOrderDraftStore(runtimeSource, clock, guard, new ProfileOrderPolicySource());
        Assert.Equivalent(quoted, await orders.FindAsync(context, quoted.OrderId, default));
        Assert.Equal(CommitOrderDraftStatus.ProgramReferenceRequired,
            (await new CommitOrderDraft(orders).ExecuteAsync(context, new(quoted.OrderId, quoted.Revision), "quoted-commit-before-ref", default)).Status);

        var setReference = new SetOrderProgramReference(orders);
        var updated = await setReference.ExecuteAsync(context,
            new(quoted.OrderId, quoted.Revision, "EVENT-PO-41"), "quoted-reference", default);
        Assert.Equal(SetOrderProgramReferenceStatus.Updated, updated.Status);
        Assert.Equal(quoted.Total, updated.Order?.Total);
        Assert.Equivalent(quoted.Lines, updated.Order?.Lines);
        Assert.Equal(quoted.QuotationOrigin, updated.Order?.QuotationOrigin);

        var committed = await new CommitOrderDraft(orders).ExecuteAsync(
            context, new(quoted.OrderId, updated.Order!.Revision), "quoted-commit", default);
        Assert.Equal(CommitOrderDraftStatus.Committed, committed.Status);
        Assert.Equal(accepted.Total, committed.Order?.Total);
        Assert.Equivalent(accepted.Lines, committed.Order?.Lines);
        Assert.Equal(accepted.Origin, committed.Order?.QuotationOrigin);
        Assert.Equal("EVENT-PO-41", committed.Order?.ExternalProgramReference);
        Assert.Equal(policyFacts, committed.Order?.ProgramPolicy);
        Assert.Equal(4, acceptedReader.ReadCount);
    }

    [Fact]
    public async Task ProgramReferenceReceiptInsertFailureRollsBackRevisionAndReferenceThenSameKeyCanRetry()
    {
        await ApplyOrderSchemaAsync();
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        await using var profileSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        var clock = new FixedTimeProvider();
        var profiles = new PostgresProfileStore(profileSource, clock);
        var context = await ResolveContextAsync(tenantId, accountId);
        _ = await PublishAndActivateProfileAsync(
            profiles, context, new(Guid.NewGuid(), Guid.NewGuid()), requireReference: true, legacyBaseline: false, "receipt-failure");

        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var runtimeSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var orders = new PostgresOrderDraftStore(runtimeSource, clock, profilePolicySource: new ProfileOrderPolicySource());
        var created = (await orders.CreateAsync(context, CreateIntent("Receipt failure order"), "receipt-failure-create", default)).Order!;
        var setReference = new SetOrderProgramReference(orders);
        var request = new SetOrderProgramReferenceRequest(created.OrderId, created.Revision, "RETRY-PO");

        await ExecuteOwnerSqlAsync("""
            CREATE FUNCTION orders.fail_program_reference_receipt_insert() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.operation = 'set-order-program-reference' THEN
                    RAISE EXCEPTION 'injected program-reference receipt failure' USING ERRCODE='P0001';
                END IF;
                RETURN NEW;
            END;
            $$;
            """);
        await ExecuteOwnerSqlAsync("""
            CREATE TRIGGER fail_program_reference_receipt_insert
                BEFORE INSERT ON orders.command_receipts
                FOR EACH ROW EXECUTE FUNCTION orders.fail_program_reference_receipt_insert();
            """);
        try
        {
            var failure = await Assert.ThrowsAsync<PostgresException>(
                () => setReference.ExecuteAsync(context, request, "reference-retry", default));
            Assert.Equal("P0001", failure.SqlState);
        }
        finally
        {
            await ExecuteOwnerSqlAsync("DROP TRIGGER IF EXISTS fail_program_reference_receipt_insert ON orders.command_receipts");
            await ExecuteOwnerSqlAsync("DROP FUNCTION IF EXISTS orders.fail_program_reference_receipt_insert()");
        }

        var afterFailure = await orders.FindAsync(context, created.OrderId, default);
        Assert.Equal(created.Revision, afterFailure?.Revision);
        Assert.Null(afterFailure?.ExternalProgramReference);
        Assert.Equal(1, await CountReceiptsAsync(tenantId));

        var retried = await setReference.ExecuteAsync(context, request, "reference-retry", default);
        Assert.Equal(SetOrderProgramReferenceStatus.Updated, retried.Status);
        Assert.Equal(created.Revision + 1, retried.Order?.Revision);
        Assert.Equal("RETRY-PO", retried.Order?.ExternalProgramReference);
        Assert.Equal(2, await CountReceiptsAsync(tenantId));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConcurrentProgramReferenceCommandsHaveOneRevisionWinner(bool sameIdempotencyKey)
    {
        await ApplyOrderSchemaAsync();
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        await using var profileSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        var clock = new FixedTimeProvider();
        var profiles = new PostgresProfileStore(profileSource, clock);
        var context = await ResolveContextAsync(tenantId, accountId);
        _ = await PublishAndActivateProfileAsync(
            profiles, context, new(Guid.NewGuid(), Guid.NewGuid()), requireReference: true, legacyBaseline: false,
            sameIdempotencyKey ? "same-reference-key" : "different-reference-keys");

        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var runtimeSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var orders = new PostgresOrderDraftStore(runtimeSource, clock, profilePolicySource: new ProfileOrderPolicySource());
        var created = (await orders.CreateAsync(context, CreateIntent("Concurrent reference order"), "concurrent-create", default)).Order!;
        var setReference = new SetOrderProgramReference(orders);
        var firstKey = "concurrent-reference-a";
        var secondKey = sameIdempotencyKey ? firstKey : "concurrent-reference-b";
        var firstRequest = new SetOrderProgramReferenceRequest(created.OrderId, created.Revision, "CONCURRENT-PO");
        var secondRequest = new SetOrderProgramReferenceRequest(created.OrderId, created.Revision, "CONCURRENT-PO");

        var results = await Task.WhenAll(
            setReference.ExecuteAsync(context, firstRequest, firstKey, default),
            setReference.ExecuteAsync(context, secondRequest, secondKey, default));

        Assert.Single(results, result => result.Status == SetOrderProgramReferenceStatus.Updated);
        Assert.Single(results, result => result.Status == (sameIdempotencyKey
            ? SetOrderProgramReferenceStatus.Replayed
            : SetOrderProgramReferenceStatus.RevisionConflict));
        var final = await orders.FindAsync(context, created.OrderId, default);
        Assert.Equal(created.Revision + 1, final?.Revision);
        Assert.Equal("CONCURRENT-PO", final?.ExternalProgramReference);
        Assert.Equal(2, await CountReceiptsAsync(tenantId));
    }

    [Fact]
    public async Task SameActorAndKeyAcrossOrdersLeavesTheLosingOrderUntouched()
    {
        await ApplyOrderSchemaAsync();
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        await using var profileSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        var clock = new FixedTimeProvider();
        var profiles = new PostgresProfileStore(profileSource, clock);
        var context = await ResolveContextAsync(tenantId, accountId);
        _ = await PublishAndActivateProfileAsync(
            profiles, context, new(Guid.NewGuid(), Guid.NewGuid()), requireReference: true, legacyBaseline: false, "cross-order-key");

        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var runtimeSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var orders = new PostgresOrderDraftStore(runtimeSource, clock, profilePolicySource: new ProfileOrderPolicySource());
        var first = (await orders.CreateAsync(context, CreateIntent("First same-key order"), "first-create", default)).Order!;
        var second = (await orders.CreateAsync(context, CreateIntent("Second same-key order"), "second-create", default)).Order!;
        var setReference = new SetOrderProgramReference(orders);

        var results = await Task.WhenAll(
            setReference.ExecuteAsync(context,
                new(first.OrderId, first.Revision, "FIRST-PO"), "shared-reference-key", default),
            setReference.ExecuteAsync(context,
                new(second.OrderId, second.Revision, "SECOND-PO"), "shared-reference-key", default));

        var winner = Assert.Single(results, result => result.Status == SetOrderProgramReferenceStatus.Updated).Order!;
        Assert.Single(results, result => result.Status == SetOrderProgramReferenceStatus.IdempotencyKeyConflict);
        var winnerId = winner.OrderId;
        var loserId = winnerId == first.OrderId ? second.OrderId : first.OrderId;
        var loserOriginal = winnerId == first.OrderId ? second : first;
        var winnerAfter = await orders.FindAsync(context, winnerId, default);
        var loserAfter = await orders.FindAsync(context, loserId, default);
        Assert.Equal(loserOriginal.Revision, loserAfter?.Revision);
        Assert.Null(loserAfter?.ExternalProgramReference);
        Assert.Equal(loserOriginal.Total, loserAfter?.Total);
        Assert.Equal(winner.Revision, winnerAfter?.Revision);
        Assert.Equal(winner.ExternalProgramReference, winnerAfter?.ExternalProgramReference);
        Assert.Equal(3, await CountReceiptsAsync(tenantId));
    }

    [Fact]
    public async Task TamperedFalsePolicyAndReceiptsConflictWithTheImmutableRetainedProfile()
    {
        await ApplyOrderSchemaAsync();
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        await using var profileSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        var clock = new FixedTimeProvider();
        var profiles = new PostgresProfileStore(profileSource, clock);
        var context = await ResolveContextAsync(tenantId, accountId);
        var retained = await PublishAndActivateProfileAsync(
            profiles, context, new(Guid.NewGuid(), Guid.NewGuid()), requireReference: true, legacyBaseline: false, "tampered-policy");

        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var runtimeSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var orders = new PostgresOrderDraftStore(runtimeSource, clock, profilePolicySource: new ProfileOrderPolicySource());
        var created = (await orders.CreateAsync(context, CreateIntent("Tamper-check order"), "tamper-create", default)).Order!;
        var setReference = new SetOrderProgramReference(orders);
        var request = new SetOrderProgramReferenceRequest(created.OrderId, created.Revision, "TAMPER-PO");
        Assert.Equal(SetOrderProgramReferenceStatus.Updated,
            (await setReference.ExecuteAsync(context, request, "tamper-reference", default)).Status);

        await ExecuteOwnerSqlAsync("ALTER TABLE orders.program_order_metadata DISABLE TRIGGER guard_program_order_metadata");
        try
        {
            await ExecuteOwnerSqlAsync(
                "UPDATE orders.program_order_metadata SET require_reference=false WHERE tenant_id=@tenant AND order_id=@order",
                ("tenant", tenantId), ("order", created.OrderId));
        }
        finally
        {
            await ExecuteOwnerSqlAsync("ALTER TABLE orders.program_order_metadata ENABLE TRIGGER guard_program_order_metadata");
        }
        await ExecuteOwnerSqlAsync("""
            UPDATE orders.command_receipts
            SET response_json=jsonb_set(response_json, '{payload,programPolicy,requireReferenceForProgramOrders}', 'false'::jsonb, false)
            WHERE tenant_id=@tenant AND order_id=@order
              AND operation IN ('create-order-draft','set-order-program-reference')
            """, ("tenant", tenantId), ("order", created.OrderId));
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var confirmCorruption = connection.CreateCommand();
            confirmCorruption.CommandText = """
                SELECT (SELECT require_reference FROM orders.program_order_metadata WHERE tenant_id=@tenant AND order_id=@order)=false,
                    count(*) FILTER (WHERE response_json#>>'{payload,programPolicy,requireReferenceForProgramOrders}'='false')
                FROM orders.command_receipts WHERE tenant_id=@tenant AND order_id=@order
                """;
            confirmCorruption.Parameters.AddWithValue("tenant", tenantId);
            confirmCorruption.Parameters.AddWithValue("order", created.OrderId);
            await using var reader = await confirmCorruption.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetBoolean(0));
            Assert.Equal(2L, reader.GetInt64(1));
        }

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => orders.FindAsync(context, created.OrderId, default));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ((IOrderDraftHistoryStore)orders).ListHistoryAsync(
                context, new GetOrderDraftHistoryRequest(created.OrderId), default));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => setReference.ExecuteAsync(context, request, "tamper-reference", default));
        Assert.True(retained.Policy.RequireReferenceForProgramOrders);
    }

    [Fact]
    public async Task MissingMetadataOnV6CreatedDraftCannotBeReboundToTheOptionalLegacyBaseline()
    {
        await ApplyOrderSchemaAsync();
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        await using var profileSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        var clock = new FixedTimeProvider();
        var profiles = new PostgresProfileStore(profileSource, clock);
        var context = await ResolveContextAsync(tenantId, accountId);
        var baseline = await PublishAndActivateProfileAsync(
            profiles, context, new(Guid.NewGuid(), Guid.NewGuid()), requireReference: false, legacyBaseline: true, "missing-v6-metadata-baseline");
        Assert.True(baseline.IsLegacyBaseline);
        Assert.False(baseline.Policy.RequireReferenceForProgramOrders);
        _ = await PublishAndActivateProfileAsync(
            profiles, context, new(Guid.NewGuid(), Guid.NewGuid()), requireReference: true, legacyBaseline: false, "missing-v6-metadata-active");

        var runtimeConnectionString = await CreateRuntimeRoleAsync();
        await using var runtimeSource = new NpgsqlDataSourceBuilder(runtimeConnectionString).Build();
        var orders = new PostgresOrderDraftStore(runtimeSource, clock, profilePolicySource: new ProfileOrderPolicySource());
        var created = (await orders.CreateAsync(context, CreateIntent("V6 anchored draft"), "v6-anchored-create", default)).Order!;
        Assert.NotNull(created.ProgramPolicy);
        Assert.True(created.ProgramPolicy!.RequireReferenceForProgramOrders);
        await using (var receiptConnection = new NpgsqlConnection(ConnectionString))
        {
            await receiptConnection.OpenAsync();
            await using var receiptVersion = receiptConnection.CreateCommand();
            receiptVersion.CommandText = "SELECT response_json->>'schemaVersion' FROM orders.command_receipts WHERE tenant_id=@tenant AND order_id=@order AND operation='create-order-draft'";
            receiptVersion.Parameters.AddWithValue("tenant", tenantId);
            receiptVersion.Parameters.AddWithValue("order", created.OrderId);
            Assert.Equal("6", await receiptVersion.ExecuteScalarAsync());
        }

        await ExecuteOwnerSqlAsync("ALTER TABLE orders.program_order_metadata DISABLE TRIGGER guard_program_order_metadata");
        try
        {
            await ExecuteOwnerSqlAsync(
                "DELETE FROM orders.program_order_metadata WHERE tenant_id=@tenant AND order_id=@order",
                ("tenant", tenantId), ("order", created.OrderId));
        }
        finally
        {
            await ExecuteOwnerSqlAsync("ALTER TABLE orders.program_order_metadata ENABLE TRIGGER guard_program_order_metadata");
        }

        var adminConnectionString = await CreateLegacyAssignmentRuntimeRoleAsync();
        await using var adminSource = new NpgsqlDataSourceBuilder(adminConnectionString).Build();
        var adminOrders = new PostgresOrderDraftStore(
            adminSource, clock, profilePolicySource: new ProfileOrderPolicySource());
        var assigned = await adminOrders.AssignLegacyPolicyAsync(
            tenantId, created.OrderId, created.Revision, Guid.NewGuid(), Guid.NewGuid(), "cannot-rebind-v6", default);

        Assert.Equal(LegacyOrderPolicyAssignmentStatus.AlreadyBound, assigned.Status);
        Assert.Equal(created.Revision, assigned.ObservedRevision);
        Assert.True(baseline.IsLegacyBaseline);
        Assert.Equal(0L, await CountRowsAsync(
            "SELECT count(*) FROM orders.program_order_metadata WHERE tenant_id=@tenant_id", tenantId));
        Assert.Equal(created.Revision, (await FindOrderRevisionAsync(tenantId, created.OrderId)));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => orders.FindAsync(context, created.OrderId, default));
    }

    [Fact]
    public async Task PinnedOptionalOrderFailsClosedWhenItsCreationReceiptDisappears()
    {
        await ApplyOrderSchemaAsync();
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await SeedAuthorityRowsAsync(accountId, tenantId);
        await using var profileSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        var clock = new FixedTimeProvider();
        var profiles = new PostgresProfileStore(profileSource, clock);
        var context = await ResolveContextAsync(tenantId, accountId);
        _ = await PublishAndActivateProfileAsync(profiles, context, new(Guid.NewGuid(), Guid.NewGuid()),
            requireReference: false, legacyBaseline: true, "missing-receipt-baseline");
        _ = await PublishAndActivateProfileAsync(profiles, context, new(Guid.NewGuid(), Guid.NewGuid()),
            requireReference: false, legacyBaseline: false, "missing-receipt-active");
        await using var runtimeSource = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var orders = new PostgresOrderDraftStore(runtimeSource, clock, profilePolicySource: new ProfileOrderPolicySource());
        var created = (await orders.CreateAsync(context, CreateIntent("Optional pinned order"), "missing-receipt-create", default)).Order!;
        await ExecuteOwnerSqlAsync("DELETE FROM orders.command_receipts WHERE tenant_id=@tenant AND order_id=@order",
            ("tenant", tenantId), ("order", created.OrderId));

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => orders.FindAsync(context, created.OrderId, default));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => new CommitOrderDraft(orders).ExecuteAsync(
            context, new(created.OrderId, created.Revision), "missing-receipt-commit", default));
        Assert.Equal(created.Revision, await FindOrderRevisionAsync(tenantId, created.OrderId));
        Assert.Equal(0L, await CountReceiptsAsync(tenantId));
    }

    private async Task<string> CreateLegacyAssignmentRuntimeRoleAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE ROLE application_orders_legacy_admin LOGIN PASSWORD 'local-legacy-admin-test-only'
                NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
            GRANT CONNECT ON DATABASE application_tests TO application_orders_legacy_admin;
            GRANT USAGE ON SCHEMA orders, profiles TO application_orders_legacy_admin;
            GRANT SELECT, INSERT ON orders.program_order_metadata TO application_orders_legacy_admin;
            GRANT SELECT ON orders.order_drafts TO application_orders_legacy_admin;
            GRANT UPDATE (revision) ON orders.order_drafts TO application_orders_legacy_admin;
            GRANT SELECT (tenant_id, order_id, operation, response_json)
                ON orders.command_receipts TO application_orders_legacy_admin;
            GRANT SELECT ON profiles.policy_heads, profiles.policy_revisions, profiles.publications,
                profiles.authority, profiles.command_receipts TO application_orders_legacy_admin;
            """;
        await command.ExecuteNonQueryAsync(CancellationToken.None);

        var runtime = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = "application_orders_legacy_admin",
            Password = "local-legacy-admin-test-only",
        };
        return runtime.ConnectionString;
    }

    private async Task ExecuteOwnerSqlAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private async Task<long> FindOrderRevisionAsync(Guid tenantId, Guid orderId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT revision FROM orders.order_drafts WHERE tenant_id=@tenant AND id=@order";
        command.Parameters.AddWithValue("tenant", tenantId);
        command.Parameters.AddWithValue("order", orderId);
        return (long)(await command.ExecuteScalarAsync(CancellationToken.None)
            ?? throw new InvalidOperationException("The Order revision was not found."));
    }

    private static async Task<TenantProfileSnapshot> PublishAndActivateProfileAsync(
        PostgresProfileStore profiles,
        TenantContext context,
        PlatformAdminAccess platformAccess,
        bool requireReference,
        bool legacyBaseline,
        string prefix)
    {
        var priorPolicy = await profiles.GetPolicyAsync(context, default);
        var edited = await profiles.EditPolicyAsync(context,
            new(priorPolicy?.Revision ?? 0, requireReference), 1, $"{prefix}-edit", default);
        Assert.Equal(ProfileCommandStatus.Edited, edited.Status);

        var policyPublished = await profiles.PublishPolicyAsync(context,
            new(edited.PolicyState!.Revision), 1, $"{prefix}-policy", default);
        Assert.Equal(ProfileCommandStatus.PolicyPublished, policyPublished.Status);
        var priorAuthority = await profiles.GetAuthorityAsync(context.TenantId, default);
        var profilePublished = await profiles.PublishProfileAsync(platformAccess,
            new(context.TenantId, priorAuthority?.Revision ?? 0, policyPublished.PublishedPolicy!.PolicyRevisionId, 1, legacyBaseline),
            $"{prefix}-profile", default);
        Assert.Equal(ProfileCommandStatus.ProfilePublished, profilePublished.Status);

        var activated = await profiles.ActivateProfileAsync(platformAccess,
            new(context.TenantId, profilePublished.Authority!.Revision, profilePublished.Profile!.ProfileId, legacyBaseline),
            $"{prefix}-activate", default);
        Assert.Equal(legacyBaseline ? ProfileCommandStatus.LegacyBaselineSelected : ProfileCommandStatus.Activated, activated.Status);
        return activated.Profile!;
    }

    private sealed class ProfileOrderPolicySource : IOrderProfilePolicySource
    {
        public async Task<OrderProgramPolicyFacts?> ResolveActiveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            Guid tenantId, CancellationToken cancellationToken) => Facts(await PostgresProfileStore.ResolveActiveForOrderAsync(
                connection, transaction, tenantId, cancellationToken));

        public async Task<OrderProgramPolicyFacts?> ResolveLegacyBaselineAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            Guid tenantId, CancellationToken cancellationToken) => Facts(await PostgresProfileStore.ResolveLegacyBaselineForOrderAsync(
                connection, transaction, tenantId, cancellationToken));

        public async Task<bool> IsRetainedCompatibleAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            Guid tenantId, OrderProgramPolicyFacts policy, CancellationToken cancellationToken) =>
            Facts(await PostgresProfileStore.ResolveRetainedForOrderAsync(connection, transaction, tenantId, policy.ProfileId, cancellationToken)) == policy;

        private static OrderProgramPolicyFacts? Facts(TenantProfileSnapshot? profile) => profile is null
            ? null
            : new(profile.ProfileId, profile.Policy.PolicyRevisionId, profile.Policy.RequireReferenceForProgramOrders);
    }

    private sealed class FixedAcceptedQuotationReader(Guid orderId, AcceptedQuotationOrder accepted) : IOrderAcceptedQuotationReader
    {
        private int _readCount;
        internal int ReadCount => Volatile.Read(ref _readCount);

        public Task<AcceptedQuotationOrder?> ReadAsync(TenantContext context, Guid requestedOrderId,
            OrderQuotationOrigin origin, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _readCount);
            return Task.FromResult<AcceptedQuotationOrder?>(requestedOrderId == orderId && origin == accepted.Origin ? accepted : null);
        }
    }
}
