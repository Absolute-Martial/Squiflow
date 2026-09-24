using Application.Customers;
using Application.Orders;
using Application.Tenancy;
using Xunit;

namespace Application.Orders.Tests;

public sealed class OrderCustomerContextTests
{
    [Fact]
    public void CustomerAttributionChangesTheCreateIntentWithoutChangingLegacyIntent()
    {
        var request = Draft();
        var organizationId = Guid.NewGuid();
        var programId = Guid.NewGuid();

        var legacy = OrderDraftIntent.Create(request);
        var organization = OrderDraftIntent.Create(request with
        {
            CustomerContext = new CustomerOrderContext(organizationId, null),
        });
        var program = OrderDraftIntent.Create(request with
        {
            CustomerContext = new CustomerOrderContext(organizationId, programId),
        });

        Assert.Null(legacy.CustomerContext);
        Assert.NotEqual(legacy.Fingerprint, organization.Fingerprint);
        Assert.NotEqual(organization.Fingerprint, program.Fingerprint);
    }

    [Fact]
    public void EmptyCustomerIdentitiesAreRejectedBeforeAnyPersistence()
    {
        var exception = Assert.Throws<OrderDraftValidationException>(() =>
            OrderDraftIntent.Create(Draft() with
            {
                CustomerContext = new CustomerOrderContext(Guid.Empty, Guid.NewGuid()),
            }));

        Assert.Equal("customer_context_invalid", exception.Code);
    }

    [Fact]
    public async Task MissingOrWrongParentCustomerContextCannotCreateAnOrder()
    {
        var customerStore = new CustomerStoreStub { Resolved = null };
        var orderStore = new OrderStoreStub();
        var operation = new CreateOrderDraft(orderStore, new ResolveCustomerOrderContext(customerStore));
        var tenant = await ContextAsync();
        var organizationId = Guid.NewGuid();
        var programId = Guid.NewGuid();

        await Assert.ThrowsAsync<CustomerOrderContextNotFoundException>(() =>
            operation.ExecuteAsync(
                tenant,
                Draft() with { CustomerContext = new CustomerOrderContext(organizationId, programId) },
                "create-1",
                CancellationToken.None));

        Assert.Equal(tenant.TenantId, customerStore.SeenTenantId);
        Assert.Equal(organizationId, customerStore.SeenOrganizationId);
        Assert.Equal(programId, customerStore.SeenProgramId);
        Assert.False(orderStore.WasCalled);
    }

    [Fact]
    public async Task ResolvedCustomerContextIsIncludedInTheOrderIntent()
    {
        var context = new CustomerOrderContext(Guid.NewGuid(), Guid.NewGuid());
        var customerStore = new CustomerStoreStub { Resolved = context };
        var orderStore = new OrderStoreStub();
        var operation = new CreateOrderDraft(orderStore, new ResolveCustomerOrderContext(customerStore));

        await operation.ExecuteAsync(
            await ContextAsync(),
            Draft() with { CustomerContext = context },
            "create-1",
            CancellationToken.None);

        Assert.Equal(context, orderStore.SeenIntent?.CustomerContext);
    }

    private static CreateOrderDraftRequest Draft() => new(
        "Program materials",
        "USD",
        [new OrderDraftLineInput("Printed panel", 1m, "EA", 25m)]);

    private static async Task<TenantContext> ContextAsync() =>
        await new ResolveTenantContext(new ActiveMembershipDirectory())
            .ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None)
        ?? throw new InvalidOperationException("The test membership was not active.");

    private sealed class ActiveMembershipDirectory : ITenantMembershipDirectory
    {
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(
            Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<bool> IsActiveAsync(
            Guid accountId, Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class CustomerStoreStub : ICustomerStore
    {
        public CustomerOrderContext? Resolved { get; init; }
        public Guid SeenTenantId { get; private set; }
        public Guid SeenOrganizationId { get; private set; }
        public Guid? SeenProgramId { get; private set; }

        public Task<CustomerOrderContext?> ResolveOrderContextAsync(
            TenantContext tenantContext, Guid organizationId, Guid? programId,
            CancellationToken cancellationToken)
        {
            SeenTenantId = tenantContext.TenantId;
            SeenOrganizationId = organizationId;
            SeenProgramId = programId;
            return Task.FromResult(Resolved);
        }

        public Task<CreateCustomerOrganizationResult> CreateOrganizationAsync(
            TenantContext tenantContext, CustomerOrganizationIntent intent, string idempotencyKey,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CreateCustomerProgramResult> CreateProgramAsync(
            TenantContext tenantContext, CustomerProgramIntent intent, string idempotencyKey,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CustomerOrganizationSnapshot?> FindOrganizationAsync(
            TenantContext tenantContext, Guid organizationId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CustomerProgramSnapshot?> FindProgramAsync(
            TenantContext tenantContext, Guid programId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CustomerOrganizationPage> ListOrganizationsAsync(
            TenantContext tenantContext, ListCustomerOrganizationsRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CustomerProgramPage> ListProgramsAsync(
            TenantContext tenantContext, ListCustomerProgramsRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class OrderStoreStub : IOrderDraftStore
    {
        public Task<ReviseOrderDraftResult> ReviseAsync(
            TenantContext tenantContext, ReviseOrderDraftRequest request, OrderDraftIntent intent,
            string idempotencyKey, string fingerprint, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public bool WasCalled { get; private set; }
        public OrderDraftIntent? SeenIntent { get; private set; }

        public Task<CreateOrderDraftResult> CreateAsync(
            TenantContext tenantContext, OrderDraftIntent intent, string idempotencyKey,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            SeenIntent = intent;
            return Task.FromResult(new CreateOrderDraftResult(CreateOrderDraftStatus.Created, null));
        }

        public Task<OrderDraftSnapshot?> FindAsync(
            TenantContext tenantContext, Guid orderId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderDraftPage> ListAsync(
            TenantContext tenantContext, ListOrderDraftsRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AbandonOrderDraftResult> AbandonAsync(
            TenantContext tenantContext, AbandonOrderDraftRequest request, string idempotencyKey,
            string fingerprint, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
