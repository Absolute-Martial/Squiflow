using Application.Orders;
using Application.Tenancy;
using Xunit;

namespace Application.Orders.Tests;

public sealed class ListOrderDraftsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task ExecuteRejectsPageSizesOutsideTheBoundedRangeBeforeCallingTheStore(int limit)
    {
        var store = new CapturingOrderDraftStore();
        var operation = new ListOrderDrafts(store);

        var exception = await Assert.ThrowsAsync<OrderDraftValidationException>(() => operation.ExecuteAsync(
            CreateTenantContext(),
            new ListOrderDraftsRequest(limit, null),
            CancellationToken.None));

        Assert.Equal("page_size_invalid", exception.Code);
        Assert.False(store.ListWasCalled);
    }

    [Theory]
    [MemberData(nameof(InvalidCursors))]
    public async Task ExecuteRejectsMalformedCursorsBeforeCallingTheStore(OrderDraftPageCursor cursor)
    {
        var store = new CapturingOrderDraftStore();
        var operation = new ListOrderDrafts(store);

        var exception = await Assert.ThrowsAsync<OrderDraftValidationException>(() => operation.ExecuteAsync(
            CreateTenantContext(),
            new ListOrderDraftsRequest(25, cursor),
            CancellationToken.None));

        Assert.Equal("cursor_invalid", exception.Code);
        Assert.False(store.ListWasCalled);
    }

    [Fact]
    public async Task ExecuteDelegatesAValidCursorAndCancellationTokenWithoutChangingThem()
    {
        var expectedItem = new OrderDraftListItem(
            Guid.NewGuid(),
            "Counter order",
            "USD",
            12.5m,
            1,
            new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero));
        var cursor = new OrderDraftPageCursor(
            new DateTimeOffset(2026, 9, 23, 11, 0, 0, TimeSpan.Zero),
            Guid.NewGuid());
        var expectedPage = new OrderDraftPage([expectedItem], cursor);
        var store = new CapturingOrderDraftStore(expectedPage);
        var operation = new ListOrderDrafts(store);
        var context = CreateTenantContext();
        using var cancellationSource = new CancellationTokenSource();
        var request = new ListOrderDraftsRequest(25, cursor);

        var page = await operation.ExecuteAsync(context, request, cancellationSource.Token);

        Assert.Same(expectedPage, page);
        Assert.True(store.ListWasCalled);
        Assert.Same(context, store.TenantContext);
        Assert.Same(request, store.Request);
        Assert.Equal(cancellationSource.Token, store.CancellationToken);
    }

    [Fact]
    public async Task ExecutePropagatesStoreCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var operation = new ListOrderDrafts(new CancelledOrderDraftStore(cancellationSource.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.ExecuteAsync(
            CreateTenantContext(),
            new ListOrderDraftsRequest(1, null),
            cancellationSource.Token));
    }

    public static TheoryData<OrderDraftPageCursor> InvalidCursors => new()
    {
        new OrderDraftPageCursor(
            new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero),
            Guid.Empty),
        new OrderDraftPageCursor(default, Guid.NewGuid()),
        new OrderDraftPageCursor(
            new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.FromHours(5)),
            Guid.NewGuid()),
    };

    private static TenantContext CreateTenantContext() =>
        new ResolveTenantContext(new ActiveMembershipDirectory())
            .ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None)
            .GetAwaiter()
            .GetResult()!;

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

    private sealed class CapturingOrderDraftStore(OrderDraftPage? page = null) : IOrderDraftStore
    {
        public bool ListWasCalled { get; private set; }

        public TenantContext? TenantContext { get; private set; }

        public ListOrderDraftsRequest? Request { get; private set; }

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
            CancellationToken cancellationToken)
        {
            ListWasCalled = true;
            TenantContext = tenantContext;
            Request = request;
            CancellationToken = cancellationToken;
            return Task.FromResult(page ?? new OrderDraftPage([], null));
        }

        public Task<AbandonOrderDraftResult> AbandonAsync(
            TenantContext tenantContext,
            AbandonOrderDraftRequest request,
            string idempotencyKey,
            string fingerprint,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class CancelledOrderDraftStore(CancellationToken expectedCancellationToken) : IOrderDraftStore
    {
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
            Task.FromCanceled<OrderDraftPage>(expectedCancellationToken);

        public Task<AbandonOrderDraftResult> AbandonAsync(
            TenantContext tenantContext,
            AbandonOrderDraftRequest request,
            string idempotencyKey,
            string fingerprint,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
