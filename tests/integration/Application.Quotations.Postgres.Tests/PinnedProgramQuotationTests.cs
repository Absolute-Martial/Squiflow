using Application.Customers;
using Application.Orders;
using Application.Orders.Postgres;
using Application.PlatformAdministration;
using Application.Profiles;
using Application.Profiles.Postgres;
using Application.Tenancy;
using Npgsql;
using Xunit;

namespace Application.Quotations.Postgres.Tests;

public sealed partial class QuotationPostgresTests
{
    [Fact]
    public async Task ActualAcceptedProgramQuotationKeepsOfferAndConversionSnapshotAcrossReferenceEditAndCommit()
    {
        var context = await Context();
        var organization = Guid.NewGuid();
        var program = Guid.NewGuid();
        await OwnerSqlAsync("""
            INSERT INTO customers.organizations(tenant_id,id,created_by_account_id,display_name,created_at)
                VALUES(@tenant,@organization,@account,'Synthetic customer',@at);
            INSERT INTO customers.programs(tenant_id,id,organization_id,created_by_account_id,display_name,created_at)
                VALUES(@tenant,@program,@organization,@account,'Synthetic program',@at);
            """, ("tenant", _tenant), ("organization", organization), ("program", program), ("account", _account), ("at", _clock.GetUtcNow()));
        await using var ownerSource = new NpgsqlDataSourceBuilder(_database.GetConnectionString()).Build();
        var profiles = new PostgresProfileStore(ownerSource, _clock);
        var edited = await profiles.EditPolicyAsync(context, new(0, true), 1, "program-policy", default);
        var policy = await profiles.PublishPolicyAsync(context, new(edited.PolicyState!.Revision), 1, "program-policy-publish", default);
        var access = new PlatformAdminAccess(Guid.NewGuid(), Guid.NewGuid());
        var publication = await profiles.PublishProfileAsync(access, new(_tenant, 0, policy.PublishedPolicy!.PolicyRevisionId, 1), "program-profile", default);
        await profiles.ActivateProfileAsync(access, new(_tenant, publication.Authority!.Revision, publication.Profile!.ProfileId), "program-activate", default);
        var quotationStore = new PostgresQuotationStore(_runtime, _clock, new PinnedOrderWriter());
        var draft = (await quotationStore.CreateDraftAsync(context, Offer() with { CustomerContext = new CustomerOrderContext(organization, program) },
            "program-quote", Fingerprint("program-quote"), default)).Quotation!;
        var issued = (await quotationStore.IssueAsync(context, draft.QuotationId, draft.Version, "program-issue", Fingerprint("program-issue"), (_, _) => Task.FromResult(true), default)).Quotation!;
        var accepted = (await quotationStore.RespondAsync(context, draft.QuotationId, QuotationResponseKind.Accepted,
            new(issued.CurrentIssued!.RevisionId, issued.Version, "Synthetic customer acceptance", "Synthetic customer"),
            "program-accept", Fingerprint("program-accept"), default)).Quotation!;
        var convert = new QuotationConvertRequest(accepted.CurrentIssued!.RevisionId, accepted.Version);
        var converted = await quotationStore.ConvertAsync(context, accepted.QuotationId, convert, "program-convert", Fingerprint("program-convert"), default);
        var original = converted.Quotation!.Conversion!.OriginalOrder;
        Assert.Equal(publication.Profile.ProfileId, original.ProgramPolicy?.ProfileId);
        Assert.True(original.ProgramPolicy!.RequireReferenceForProgramOrders);
        var orders = new PostgresOrderDraftStore(_runtime, _clock, new OrderCommercialCommitGuard(null!, null!, null!, quotationStore), profilePolicySource: new PinnedProfileBridge());
        var commit = new CommitOrderDraft(orders);
        Assert.Equal(CommitOrderDraftStatus.ProgramReferenceRequired,
            (await commit.ExecuteAsync(context, new(original.OrderId, original.Revision), "program-commit", default)).Status);
        var reference = await new SetOrderProgramReference(orders).ExecuteAsync(context, new(original.OrderId, original.Revision, "PO-1042"), "program-reference", default);
        Assert.Equal(SetOrderProgramReferenceStatus.Updated, reference.Status);
        var committed = await commit.ExecuteAsync(context, new(original.OrderId, reference.Order!.Revision), "program-commit", default);
        Assert.Equal(CommitOrderDraftStatus.Committed, committed.Status);
        Assert.Equivalent(original.Lines, committed.Order!.Lines);
        Assert.Equal(original.Total, committed.Order.Total);
        Assert.Equal(original.QuotationOrigin, committed.Order.QuotationOrigin);
        Assert.Equal("PO-1042", committed.Order.ExternalProgramReference);
        var replay = await quotationStore.ConvertAsync(context, accepted.QuotationId, convert, "program-convert", Fingerprint("program-convert"), default);
        var relink = await quotationStore.ConvertAsync(context, accepted.QuotationId, convert, "program-convert-again", Fingerprint("program-convert-again"), default);
        Assert.Equal(QuotationCommandStatus.Replayed, replay.Status);
        Assert.Equal(QuotationCommandStatus.AlreadyLinked, relink.Status);
        Assert.Equivalent(original, replay.Quotation!.Conversion!.OriginalOrder);
        Assert.Equivalent(original, relink.Quotation!.Conversion!.OriginalOrder);
        var history = await orders.ListHistoryAsync(context, new(original.OrderId, 10, null), default);
        Assert.Equal(3, history!.Items.Count);
        Assert.Equal(OrderDraftChange.ProgramReferenceUpdated, history.Items[1].Change);
    }

    private sealed class PinnedOrderWriter : IQuotationOrderWriter
    {
        public async Task<OrderDraftSnapshot> CreateAsync(TenantContext context, AcceptedQuotationOrder accepted,
            NpgsqlConnection connection, NpgsqlTransaction transaction, DateTimeOffset createdAt, CancellationToken ct)
        {
            var profile = await PostgresProfileStore.ResolveActiveForOrderAsync(connection, transaction, context.TenantId, ct)
                ?? throw new OrderProfileUnavailableException();
            return await PostgresOrderDraftStore.CreateAcceptedQuotationAsync(context, accepted, connection, transaction, createdAt, ct,
                new(profile.ProfileId, profile.Policy.PolicyRevisionId, profile.Policy.RequireReferenceForProgramOrders));
        }
    }

    private sealed class PinnedProfileBridge : IOrderProfilePolicySource
    {
        public async Task<OrderProgramPolicyFacts?> ResolveActiveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, CancellationToken ct) =>
            Facts(await PostgresProfileStore.ResolveActiveForOrderAsync(connection, transaction, tenantId, ct));
        public async Task<OrderProgramPolicyFacts?> ResolveLegacyBaselineAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, CancellationToken ct) =>
            Facts(await PostgresProfileStore.ResolveLegacyBaselineForOrderAsync(connection, transaction, tenantId, ct));
        public async Task<bool> IsRetainedCompatibleAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, OrderProgramPolicyFacts policy, CancellationToken ct) =>
            Facts(await PostgresProfileStore.ResolveRetainedForOrderAsync(connection, transaction, tenantId, policy.ProfileId, ct)) == policy;
        private static OrderProgramPolicyFacts? Facts(TenantProfileSnapshot? profile) => profile is null ? null :
            new(profile.ProfileId, profile.Policy.PolicyRevisionId, profile.Policy.RequireReferenceForProgramOrders);
    }
}
