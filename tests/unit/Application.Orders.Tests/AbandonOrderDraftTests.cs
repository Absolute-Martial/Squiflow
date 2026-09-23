using System.Text.Json;
using Application.Orders;
using Application.Tenancy;
using Xunit;

namespace Application.Orders.Tests;

public sealed class AbandonOrderDraftTests
{
    private static readonly JsonSerializerOptions ReceiptJsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(0, "expected_revision_invalid")]
    [InlineData(-1, "expected_revision_invalid")]
    public async Task InvalidExpectedRevisionNeverCallsTheStore(long revision, string expectedCode)
    {
        var store = new CapturingStore();
        var operation = new AbandonOrderDraft(store);
        var context = await CreateContextAsync();

        var error = await Assert.ThrowsAsync<OrderDraftValidationException>(() => operation.ExecuteAsync(
            context,
            new AbandonOrderDraftRequest(Guid.NewGuid(), revision),
            "abandon-operation-1",
            CancellationToken.None));

        Assert.Equal(expectedCode, error.Code);
        Assert.False(store.WasCalled);
    }

    [Fact]
    public async Task EmptyOrderIdentityAndMalformedIdempotencyKeyNeverCallTheStore()
    {
        var store = new CapturingStore();
        var operation = new AbandonOrderDraft(store);
        var context = await CreateContextAsync();

        var emptyOrder = await Assert.ThrowsAsync<OrderDraftValidationException>(() => operation.ExecuteAsync(
            context,
            new AbandonOrderDraftRequest(Guid.Empty, 1),
            "abandon-operation-1",
            CancellationToken.None));
        var malformedKey = await Assert.ThrowsAsync<OrderDraftValidationException>(() => operation.ExecuteAsync(
            context,
            new AbandonOrderDraftRequest(Guid.NewGuid(), 1),
            "key\uD800",
            CancellationToken.None));

        Assert.Equal("order_id_invalid", emptyOrder.Code);
        Assert.Equal("idempotency_key_invalid", malformedKey.Code);
        Assert.False(store.WasCalled);
    }

    [Fact]
    public async Task EquivalentAbandonIntentHasStableFingerprintAndPreservesCancellation()
    {
        var store = new CapturingStore();
        var operation = new AbandonOrderDraft(store);
        var context = await CreateContextAsync();
        var orderId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();

        await operation.ExecuteAsync(
            context,
            new AbandonOrderDraftRequest(orderId, 1),
            "  abandon-operation-1  ",
            cancellation.Token);
        var firstFingerprint = store.Fingerprint;
        Assert.Equal("abandon-operation-1", store.IdempotencyKey);
        Assert.Equal(cancellation.Token, store.CancellationToken);
        Assert.Same(context, store.TenantContext);

        await operation.ExecuteAsync(
            context,
            new AbandonOrderDraftRequest(orderId, 1),
            "abandon-operation-1",
            cancellation.Token);
        Assert.Equal(firstFingerprint, store.Fingerprint);
        Assert.Matches("^[0-9a-f]{64}$", firstFingerprint!);

        await operation.ExecuteAsync(
            context,
            new AbandonOrderDraftRequest(orderId, 2),
            "abandon-operation-1",
            cancellation.Token);
        Assert.NotEqual(firstFingerprint, store.Fingerprint);
    }

    [Fact]
    public void HistoricalCreationReceiptWithoutLifecycleFieldsDeserializesAsDraft()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var json = $$"""
            {
              "orderId": "{{orderId:D}}",
              "tenantId": "{{tenantId:D}}",
              "createdByAccountId": "{{accountId:D}}",
              "summary": "Counter order",
              "currencyCode": "USD",
              "total": 12.5,
              "revision": 1,
              "createdAt": "2026-09-23T00:00:00+00:00",
              "lines": []
            }
            """;

        var snapshot = JsonSerializer.Deserialize<OrderDraftSnapshot>(
            json,
            ReceiptJsonOptions);

        Assert.NotNull(snapshot);
        Assert.Equal(OrderDraftState.Draft, snapshot.State);
        Assert.Null(snapshot.AbandonedAt);
        Assert.Null(snapshot.AbandonedByAccountId);
    }

    [Theory]
    [InlineData(OrderDraftState.Draft, 1, 1, AbandonOrderDraftStatus.Abandoned)]
    [InlineData(OrderDraftState.Draft, 1, 2, AbandonOrderDraftStatus.RevisionConflict)]
    [InlineData(OrderDraftState.Abandoned, 2, 1, AbandonOrderDraftStatus.AlreadyAbandoned)]
    [InlineData(OrderDraftState.Abandoned, 2, 2, AbandonOrderDraftStatus.AlreadyAbandoned)]
    public void AbandonmentDecisionRespectsCurrentStateBeforeRevision(
        OrderDraftState state,
        long currentRevision,
        long expectedRevision,
        AbandonOrderDraftStatus expectedStatus)
    {
        Assert.Equal(
            expectedStatus,
            OrderDraftLifecycle.AssessAbandon(state, currentRevision, expectedRevision));
    }

    private static async Task<TenantContext> CreateContextAsync() =>
        (await new ResolveTenantContext(new ActiveMembershipDirectory())
            .ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None))!;

    private sealed class ActiveMembershipDirectory : ITenantMembershipDirectory
    {
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(
            Guid accountId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<bool> IsActiveAsync(
            Guid accountId,
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class CapturingStore : IOrderDraftStore
    {
        public bool WasCalled { get; private set; }

        public TenantContext? TenantContext { get; private set; }

        public string? IdempotencyKey { get; private set; }

        public string? Fingerprint { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<CreateOrderDraftResult> CreateAsync(
            TenantContext tenantContext,
            OrderDraftIntent intent,
            string idempotencyKey,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<OrderDraftSnapshot?> FindAsync(
            TenantContext tenantContext,
            Guid orderId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<OrderDraftPage> ListAsync(
            TenantContext tenantContext,
            ListOrderDraftsRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AbandonOrderDraftResult> AbandonAsync(
            TenantContext tenantContext,
            AbandonOrderDraftRequest request,
            string idempotencyKey,
            string fingerprint,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            TenantContext = tenantContext;
            IdempotencyKey = idempotencyKey;
            Fingerprint = fingerprint;
            CancellationToken = cancellationToken;
            return Task.FromResult(new AbandonOrderDraftResult(AbandonOrderDraftStatus.NotFound, null));
        }
    }
}
