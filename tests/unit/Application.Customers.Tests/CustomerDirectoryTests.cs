using Application.Customers;
using Application.Tenancy;
using Xunit;

namespace Application.Customers.Tests;

public sealed class CustomerDirectoryTests
{
    [Fact]
    public async Task OrganizationCreateNormalizesNameAndKeyBeforePassingStableIntent()
    {
        var store = new CapturingStore();
        var context = await CreateTenantContextAsync();
        var command = new CreateCustomerOrganization(store);

        await command.ExecuteAsync(
            context,
            new CreateCustomerOrganizationRequest("  Cafe\u0301  "),
            "  receipt-1  ",
            CancellationToken.None);
        var fingerprint = store.OrganizationIntent!.Fingerprint;

        Assert.Equal("Café", store.OrganizationIntent.DisplayName);
        Assert.Equal("receipt-1", store.Key);
        Assert.Same(context, store.Context);
        await command.ExecuteAsync(
            context,
            new CreateCustomerOrganizationRequest("Café"),
            "receipt-2",
            CancellationToken.None);
        Assert.Equal(fingerprint, store.OrganizationIntent.Fingerprint);
    }

    [Fact]
    public async Task ProgramIntentFingerprintBindsImmutableParent()
    {
        var store = new CapturingStore();
        var context = await CreateTenantContextAsync();
        var command = new CreateCustomerProgram(store);
        var firstParent = Guid.NewGuid();
        var secondParent = Guid.NewGuid();

        await command.ExecuteAsync(
            context,
            new CreateCustomerProgramRequest(firstParent, " Program A "),
            "receipt-1",
            CancellationToken.None);
        var firstFingerprint = store.ProgramIntent!.Fingerprint;
        Assert.Equal(firstParent, store.ProgramIntent.OrganizationId);
        Assert.Equal("Program A", store.ProgramIntent.DisplayName);

        await command.ExecuteAsync(
            context,
            new CreateCustomerProgramRequest(secondParent, "Program A"),
            "receipt-1",
            CancellationToken.None);
        Assert.NotEqual(firstFingerprint, store.ProgramIntent!.Fingerprint);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid\nname")]
    public async Task CreateRejectsInvalidNamesBeforeCallingStore(string name)
    {
        var store = new CapturingStore();
        var context = await CreateTenantContextAsync();
        var exception = await Assert.ThrowsAsync<CustomerValidationException>(() =>
            new CreateCustomerOrganization(store).ExecuteAsync(
                context,
                new CreateCustomerOrganizationRequest(name),
                "receipt-1",
                CancellationToken.None));

        Assert.Equal("display_name_invalid", exception.Code);
        Assert.Null(store.OrganizationIntent);
    }

    [Fact]
    public async Task CreateRejectsMalformedUtf16BeforeCallingStore()
    {
        var store = new CapturingStore();
        var context = await CreateTenantContextAsync();
        var malformed = new string((char)0xD800, 1);

        var exception = await Assert.ThrowsAsync<CustomerValidationException>(() =>
            new CreateCustomerOrganization(store).ExecuteAsync(
                context,
                new CreateCustomerOrganizationRequest(malformed),
                "receipt-1",
                CancellationToken.None));

        Assert.Equal("display_name_invalid", exception.Code);
        Assert.Null(store.OrganizationIntent);
    }

    [Fact]
    public async Task CreateRejectsOversizedNameAndKey()
    {
        var store = new CapturingStore();
        var context = await CreateTenantContextAsync();
        var command = new CreateCustomerOrganization(store);

        var nameError = await Assert.ThrowsAsync<CustomerValidationException>(() => command.ExecuteAsync(
            context,
            new CreateCustomerOrganizationRequest(new string('a', 201)),
            "receipt-1",
            CancellationToken.None));
        var keyError = await Assert.ThrowsAsync<CustomerValidationException>(() => command.ExecuteAsync(
            context,
            new CreateCustomerOrganizationRequest("Valid"),
            new string('a', 129),
            CancellationToken.None));

        Assert.Equal("display_name_invalid", nameError.Code);
        Assert.Equal("idempotency_key_invalid", keyError.Code);
        Assert.Null(store.OrganizationIntent);
    }

    [Fact]
    public async Task ProgramCreateRejectsEmptyParentIdentity()
    {
        var store = new CapturingStore();
        var context = await CreateTenantContextAsync();
        var exception = await Assert.ThrowsAsync<CustomerValidationException>(() =>
            new CreateCustomerProgram(store).ExecuteAsync(
                context,
                new CreateCustomerProgramRequest(Guid.Empty, "Program"),
                "receipt-1",
                CancellationToken.None));

        Assert.Equal("organization_id_invalid", exception.Code);
        Assert.Null(store.ProgramIntent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task ListsRejectPageSizesOutsideOneToFifty(int limit)
    {
        var context = await CreateTenantContextAsync();
        var store = new CapturingStore();
        var organizationError = await Assert.ThrowsAsync<CustomerValidationException>(() =>
            new ListCustomerOrganizations(store).ExecuteAsync(
                context,
                new ListCustomerOrganizationsRequest(limit, null),
                CancellationToken.None));
        var programError = await Assert.ThrowsAsync<CustomerValidationException>(() =>
            new ListCustomerPrograms(store).ExecuteAsync(
                context,
                new ListCustomerProgramsRequest(Guid.NewGuid(), limit, null),
                CancellationToken.None));

        Assert.Equal("page_size_invalid", organizationError.Code);
        Assert.Equal("page_size_invalid", programError.Code);
        Assert.Null(store.Context);
    }

    [Fact]
    public async Task ListsRejectMalformedCursors()
    {
        var context = await CreateTenantContextAsync();
        var store = new CapturingStore();
        var timestamp = new DateTimeOffset(2026, 9, 24, 10, 0, 0, TimeSpan.FromHours(5));

        var organizationError = await Assert.ThrowsAsync<CustomerValidationException>(() =>
            new ListCustomerOrganizations(store).ExecuteAsync(
                context,
                new ListCustomerOrganizationsRequest(10, new CustomerOrganizationPageCursor(timestamp, Guid.NewGuid())),
                CancellationToken.None));
        var programError = await Assert.ThrowsAsync<CustomerValidationException>(() =>
            new ListCustomerPrograms(store).ExecuteAsync(
                context,
                new ListCustomerProgramsRequest(
                    Guid.NewGuid(), 10, new CustomerProgramPageCursor(timestamp, Guid.NewGuid())),
                CancellationToken.None));

        Assert.Equal("cursor_invalid", organizationError.Code);
        Assert.Equal("cursor_invalid", programError.Code);
        Assert.Null(store.Context);
    }

    [Fact]
    public async Task OrderContextResolverPassesTenantAndParentToStore()
    {
        var context = await CreateTenantContextAsync();
        var organizationId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var expected = new CustomerOrderContext(organizationId, programId);
        var store = new CapturingStore { ResolvedContext = expected };
        using var cancellation = new CancellationTokenSource();

        var actual = await new ResolveCustomerOrderContext(store).ExecuteAsync(
            context,
            organizationId,
            programId,
            cancellation.Token);

        Assert.Same(expected, actual);
        Assert.Same(context, store.Context);
        Assert.Equal(organizationId, store.OrganizationId);
        Assert.Equal(programId, store.ProgramId);
        Assert.Equal(cancellation.Token, store.CancellationToken);
    }

    private static async Task<TenantContext> CreateTenantContextAsync() =>
        (await new ResolveTenantContext(new ActiveMembershipDirectory()).ExecuteAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None))!;

    private sealed class ActiveMembershipDirectory : ITenantMembershipDirectory
    {
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(
            Guid accountId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<bool> IsActiveAsync(
            Guid accountId,
            Guid tenantId,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class CapturingStore : ICustomerStore
    {
        public TenantContext? Context { get; private set; }

        public CustomerOrganizationIntent? OrganizationIntent { get; private set; }

        public CustomerProgramIntent? ProgramIntent { get; private set; }

        public string? Key { get; private set; }

        public Guid OrganizationId { get; private set; }

        public Guid? ProgramId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public CustomerOrderContext? ResolvedContext { get; init; }

        public Task<CreateCustomerOrganizationResult> CreateOrganizationAsync(
            TenantContext tenantContext,
            CustomerOrganizationIntent intent,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            Context = tenantContext;
            OrganizationIntent = intent;
            Key = idempotencyKey;
            return Task.FromResult(new CreateCustomerOrganizationResult(
                CreateCustomerOrganizationStatus.IdempotencyKeyConflict, null));
        }

        public Task<CreateCustomerProgramResult> CreateProgramAsync(
            TenantContext tenantContext,
            CustomerProgramIntent intent,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            Context = tenantContext;
            ProgramIntent = intent;
            Key = idempotencyKey;
            return Task.FromResult(new CreateCustomerProgramResult(
                CreateCustomerProgramStatus.IdempotencyKeyConflict, null));
        }

        public Task<CustomerOrganizationSnapshot?> FindOrganizationAsync(
            TenantContext tenantContext,
            Guid organizationId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CustomerProgramSnapshot?> FindProgramAsync(
            TenantContext tenantContext,
            Guid programId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CustomerOrganizationPage> ListOrganizationsAsync(
            TenantContext tenantContext,
            ListCustomerOrganizationsRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CustomerProgramPage> ListProgramsAsync(
            TenantContext tenantContext,
            ListCustomerProgramsRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CustomerOrderContext?> ResolveOrderContextAsync(
            TenantContext tenantContext,
            Guid organizationId,
            Guid? programId,
            CancellationToken cancellationToken)
        {
            Context = tenantContext;
            OrganizationId = organizationId;
            ProgramId = programId;
            CancellationToken = cancellationToken;
            return Task.FromResult(ResolvedContext);
        }
    }
}
