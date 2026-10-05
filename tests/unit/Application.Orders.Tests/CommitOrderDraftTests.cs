using System.Security.Cryptography;
using System.Text;
using Application.Orders;
using Application.Tenancy;
using Xunit;

namespace Application.Orders.Tests;

public sealed class CommitOrderDraftTests
{
    [Fact]
    public async Task CommitNormalizesKeyAndForwardsDeterministicRevisionFingerprintAndCancellation()
    {
        var store = new RecordingStore();
        var command = new CommitOrderDraft(store);
        var context = await CreateContextAsync();
        var request = new CommitOrderDraftRequest(Guid.NewGuid(), 17);
        using var cancellation = new CancellationTokenSource();
        var expectedResult = new CommitOrderDraftResult(CommitOrderDraftStatus.Replayed, null);
        store.Result = expectedResult;

        var result = await command.ExecuteAsync(context, request, "  retry-1  ", cancellation.Token);

        Assert.Same(expectedResult, result);
        Assert.Same(context, store.TenantContext);
        Assert.Same(request, store.Request);
        Assert.Equal("retry-1", store.IdempotencyKey);
        Assert.Equal(Fingerprint(request), store.Fingerprint);
        Assert.Equal(cancellation.Token, store.CancellationToken);
    }

    [Fact]
    public async Task FingerprintChangesWithRevisionAndRepeatedRequestIsStable()
    {
        var store = new RecordingStore();
        var command = new CommitOrderDraft(store);
        var context = await CreateContextAsync();
        var orderId = Guid.NewGuid();

        await command.ExecuteAsync(context, new CommitOrderDraftRequest(orderId, 2), "key", default);
        var fingerprint = store.Fingerprint;
        await command.ExecuteAsync(context, new CommitOrderDraftRequest(orderId, 2), "key", default);
        Assert.Equal(fingerprint, store.Fingerprint);
        await command.ExecuteAsync(context, new CommitOrderDraftRequest(orderId, 3), "key", default);
        Assert.NotEqual(fingerprint, store.Fingerprint);
    }

    [Fact]
    public async Task InvalidOrderIdentityDoesNotCallStore()
    {
        var store = new RecordingStore();
        var command = new CommitOrderDraft(store);
        var context = await CreateContextAsync();
        var error = await Assert.ThrowsAsync<OrderDraftValidationException>(() => command.ExecuteAsync(
            context, new CommitOrderDraftRequest(Guid.Empty, 1), "key", default));
        Assert.Equal("order_id_invalid", error.Code);
        Assert.False(store.WasCalled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidExpectedRevisionDoesNotCallStore(long revision)
    {
        var store = new RecordingStore();
        var command = new CommitOrderDraft(store);
        var context = await CreateContextAsync();
        var error = await Assert.ThrowsAsync<OrderDraftValidationException>(() => command.ExecuteAsync(
            context, new CommitOrderDraftRequest(Guid.NewGuid(), revision), "key", default));
        Assert.Equal("expected_revision_invalid", error.Code);
        Assert.False(store.WasCalled);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("bad\nkey")]
    public async Task InvalidIdempotencyKeyDoesNotCallStore(string key)
    {
        var store = new RecordingStore();
        var command = new CommitOrderDraft(store);
        var context = await CreateContextAsync();
        var error = await Assert.ThrowsAsync<OrderDraftValidationException>(() => command.ExecuteAsync(
            context, new CommitOrderDraftRequest(Guid.NewGuid(), 1), key, default));
        Assert.Equal("idempotency_key_invalid", error.Code);
        Assert.False(store.WasCalled);
    }

    [Fact]
    public void OldSnapshotJsonKeepsDraftDefaultsAndNewMetadataNull()
    {
        const string json = """
            {"OrderId":"11111111-1111-1111-1111-111111111111","TenantId":"22222222-2222-2222-2222-222222222222","CreatedByAccountId":"33333333-3333-3333-3333-333333333333","Summary":"Order","CurrencyCode":"USD","Total":1,"Revision":1,"CreatedAt":"2026-10-01T00:00:00+00:00","Lines":[]}
            """;

        var snapshot = System.Text.Json.JsonSerializer.Deserialize<OrderDraftSnapshot>(json);

        Assert.NotNull(snapshot);
        Assert.Equal(OrderDraftState.Draft, snapshot.State);
        Assert.Null(snapshot.CommittedAt);
        Assert.Null(snapshot.CommittedByAccountId);

        const string listItemJson = """
            {"OrderId":"11111111-1111-1111-1111-111111111111","Summary":"Order","CurrencyCode":"USD","Total":1,"Revision":1,"CreatedAt":"2026-10-01T00:00:00+00:00"}
            """;
        var item = System.Text.Json.JsonSerializer.Deserialize<OrderDraftListItem>(listItemJson);
        Assert.NotNull(item);
        Assert.Equal(OrderDraftState.Draft, item.State);
        Assert.Null(item.CommittedAt);
    }

    [Theory]
    [InlineData(OrderDraftState.Draft, 5, 5, CommitOrderDraftStatus.Committed)]
    [InlineData(OrderDraftState.Draft, 6, 5, CommitOrderDraftStatus.RevisionConflict)]
    [InlineData(OrderDraftState.Committed, 5, 5, CommitOrderDraftStatus.AlreadyCommitted)]
    [InlineData(OrderDraftState.Abandoned, 5, 5, CommitOrderDraftStatus.AlreadyAbandoned)]
    public void LifecycleAssessesCommitState(
        OrderDraftState state,
        long currentRevision,
        long expectedRevision,
        CommitOrderDraftStatus expected) =>
        Assert.Equal(expected, OrderDraftLifecycle.AssessCommit(state, currentRevision, expectedRevision));

    [Fact]
    public void ReviseAndAbandonRejectCommittedState()
    {
        Assert.Equal(ReviseOrderDraftStatus.AlreadyCommitted,
            OrderDraftLifecycle.AssessRevise(OrderDraftState.Committed, 1, 1));
        Assert.Equal(AbandonOrderDraftStatus.AlreadyCommitted,
            OrderDraftLifecycle.AssessAbandon(OrderDraftState.Committed, 1, 1));
    }

    [Fact]
    public void LifecycleRejectsUnknownState()
    {
        var exCommit = Assert.Throws<InvalidOperationException>(() =>
            OrderDraftLifecycle.AssessCommit((OrderDraftState)99, 1, 1));
        Assert.Equal("The order draft has an unsupported state.", exCommit.Message);

        var exRevise = Assert.Throws<InvalidOperationException>(() =>
            OrderDraftLifecycle.AssessRevise((OrderDraftState)99, 1, 1));
        Assert.Equal("The order draft has an unsupported state.", exRevise.Message);

        var exAbandon = Assert.Throws<InvalidOperationException>(() =>
            OrderDraftLifecycle.AssessAbandon((OrderDraftState)99, 1, 1));
        Assert.Equal("The order draft has an unsupported state.", exAbandon.Message);
    }

    private static string Fingerprint(CommitOrderDraftRequest request)
    {
        var canonical = $"v1:commit-order-draft:{request.OrderId:N}:{request.ExpectedRevision}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static async Task<TenantContext> CreateContextAsync() =>
        (await new ResolveTenantContext(new ActiveMembershipDirectory())
            .ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None))!;

    private sealed class ActiveMembershipDirectory : ITenantMembershipDirectory
    {
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<bool> IsActiveAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class RecordingStore : IOrderDraftStore
    {
        public bool WasCalled { get; private set; }
        public TenantContext? TenantContext { get; private set; }
        public CommitOrderDraftRequest? Request { get; private set; }
        public string? IdempotencyKey { get; private set; }
        public string? Fingerprint { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public CommitOrderDraftResult Result { get; set; } = new(CommitOrderDraftStatus.Replayed, null);

        public Task<CommitOrderDraftResult> CommitAsync(
            TenantContext tenantContext, CommitOrderDraftRequest request, string idempotencyKey,
            string fingerprint, CancellationToken cancellationToken)
        {
            WasCalled = true;
            TenantContext = tenantContext;
            Request = request;
            IdempotencyKey = idempotencyKey;
            Fingerprint = fingerprint;
            CancellationToken = cancellationToken;
            return Task.FromResult(Result);
        }

        public Task<CreateOrderDraftResult> CreateAsync(TenantContext tenantContext, OrderDraftIntent intent,
            string idempotencyKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrderDraftSnapshot?> FindAsync(TenantContext tenantContext, Guid orderId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrderDraftPage> ListAsync(TenantContext tenantContext, ListOrderDraftsRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AbandonOrderDraftResult> AbandonAsync(TenantContext tenantContext,
            AbandonOrderDraftRequest request, string idempotencyKey, string fingerprint,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReviseOrderDraftResult> ReviseAsync(TenantContext tenantContext,
            ReviseOrderDraftRequest request, OrderDraftIntent intent, string idempotencyKey,
            string fingerprint, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
