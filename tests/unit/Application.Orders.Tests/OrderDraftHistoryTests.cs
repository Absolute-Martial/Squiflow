using Application.Orders;
using Application.Tenancy;
using Xunit;

namespace Application.Orders.Tests;

public sealed class OrderDraftHistoryTests
{
    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidRequestNeverCallsTheStore(
        GetOrderDraftHistoryRequest request,
        string expectedCode)
    {
        var store = new RecordingStore();
        var operation = new GetOrderDraftHistory(store);

        var error = await Assert.ThrowsAsync<OrderDraftValidationException>(() => operation.ExecuteAsync(
            CreateTenantContext(), request, CancellationToken.None));

        Assert.Equal(expectedCode, error.Code);
        Assert.False(store.WasCalled);
    }

    [Fact]
    public async Task ValidRequestForwardsContextRequestAndCancellationAndReturnsNull()
    {
        var store = new RecordingStore();
        var operation = new GetOrderDraftHistory(store);
        var context = CreateTenantContext();
        var request = new GetOrderDraftHistoryRequest(Guid.NewGuid(), 10, 4);
        using var cancellation = new CancellationTokenSource();

        var result = await operation.ExecuteAsync(context, request, cancellation.Token);

        Assert.Null(result);
        Assert.True(store.WasCalled);
        Assert.Same(context, store.TenantContext);
        Assert.Same(request, store.Request);
        Assert.Equal(cancellation.Token, store.CancellationToken);
    }

    public static TheoryData<GetOrderDraftHistoryRequest, string> InvalidRequests => new()
    {
        { new GetOrderDraftHistoryRequest(Guid.Empty), "order_id_invalid" },
        { new GetOrderDraftHistoryRequest(Guid.NewGuid(), 0), "limit_invalid" },
        { new GetOrderDraftHistoryRequest(Guid.NewGuid(), 11), "limit_invalid" },
        { new GetOrderDraftHistoryRequest(Guid.NewGuid(), 5, 0), "history_revision_invalid" },
        { new GetOrderDraftHistoryRequest(Guid.NewGuid(), 5, -1), "history_revision_invalid" },
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

    private sealed class RecordingStore : IOrderDraftHistoryStore
    {
        public bool WasCalled { get; private set; }
        public TenantContext? TenantContext { get; private set; }
        public GetOrderDraftHistoryRequest? Request { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<OrderDraftHistoryPage?> ListHistoryAsync(
            TenantContext tenantContext,
            GetOrderDraftHistoryRequest request,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            TenantContext = tenantContext;
            Request = request;
            CancellationToken = cancellationToken;
            return Task.FromResult<OrderDraftHistoryPage?>(null);
        }
    }
}
