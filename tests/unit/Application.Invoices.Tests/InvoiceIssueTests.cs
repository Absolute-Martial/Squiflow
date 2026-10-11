using Application.Customers;
using Application.Invoices;
using Application.Orders;
using Application.Tenancy;
using Xunit;

namespace Application.Invoices.Tests;

public sealed class InvoiceIssueTests
{
    [Fact]
    public async Task ValidDefaultOrganizationIssueFreezesCommittedNprFactsAndUsesToEvenArithmetic()
    {
        var tenant = await CreateContextAsync();
        var organizationId = Guid.NewGuid();
        var orderStore = new TestOrderStore
        {
            Snapshot = CommittedOrder(
                tenant,
                currencyCode: "NPR",
                customerContext: new CustomerOrderContext(organizationId, null),
                lines:
                [
                    new OrderDraftLine(1, "Precise line", 0.5m, "EA", 2.4689m, 1.2344m),
                    new OrderDraftLine(2, "Whole line", 2m, "EA", 5m, 10m),
                ],
                total: 11.2344m),
        };
        var customers = new TestCustomerStore();
        customers.Organizations[organizationId] = Organization(tenant, organizationId, "Acme Organization");
        var authority = new TestAuthority();
        var invoiceStore = new InMemoryInvoiceIssueStore();
        var useCase = CreateUseCase(authority, invoiceStore, orderStore, customers);
        var request = new IssueInvoiceRequest(
            orderStore.Snapshot.OrderId,
            orderStore.Snapshot.Revision,
            null,
            "  issue-1  ");

        var result = await useCase.ExecuteAsync(tenant, request, CancellationToken.None);

        Assert.Equal(InvoiceIssueStatus.Issued, result.Status);
        var invoice = Assert.IsType<InvoiceIssuedFacts>(result.Invoice);
        Assert.Equal(tenant.TenantId, invoice.TenantId);
        Assert.Equal(orderStore.Snapshot.OrderId, invoice.SourceOrderId);
        Assert.Equal(orderStore.Snapshot.Revision, invoice.SourceOrderRevision);
        Assert.Equal("NPR", invoice.CurrencyCode);
        Assert.Equal(1.2344m, invoice.Lines[0].LineTotal);
        Assert.Equal(11.2344m, invoice.Total);
        var debtor = Assert.IsType<InvoiceDebtorFacts.Organization>(invoice.Debtor);
        Assert.Equal(organizationId, debtor.OrganizationId);
        Assert.Equal("Acme Organization", debtor.OrganizationDisplayName);
        Assert.Equal(tenant.AccountId, invoice.IssuedByAccountId);
        Assert.Equal(TimeSpan.Zero, invoice.IssuedAt.Offset);
        Assert.Equal(new DateOnly(2026, 10, 3), invoice.BusinessDate);
        Assert.Equal("ORG-TEST-0001", invoice.BusinessReference);
        Assert.Equal(1, authority.CheckCount);
        Assert.Equal(1, invoiceStore.CommitCount);
        Assert.Equal("issue-1", invoiceStore.LastNormalizedKey);
    }

    [Fact]
    public async Task ProgramSelectionRejectsAProgramWhoseOwningOrganizationDoesNotMatchCommittedAttribution()
    {
        var tenant = await CreateContextAsync();
        var committedOrganizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var orderStore = new TestOrderStore
        {
            Snapshot = CommittedOrder(
                tenant,
                customerContext: new CustomerOrderContext(committedOrganizationId, programId)),
        };
        var customers = new TestCustomerStore();
        customers.Programs[programId] = Program(tenant, programId, otherOrganizationId, "Wrong parent program");
        customers.Organizations[committedOrganizationId] = Organization(
            tenant, committedOrganizationId, "Committed organization");
        var invoiceStore = new InMemoryInvoiceIssueStore();
        var useCase = CreateUseCase(new TestAuthority(), invoiceStore, orderStore, customers);

        var result = await useCase.ExecuteAsync(
            tenant,
            new IssueInvoiceRequest(
                orderStore.Snapshot.OrderId,
                orderStore.Snapshot.Revision,
                new InvoiceDebtorSelection.AttributedProgram(),
                "program-mismatch"),
            CancellationToken.None);

        Assert.Equal(InvoiceIssueStatus.InvalidDebtor, result.Status);
        Assert.Null(result.Invoice);
        Assert.Equal(0, invoiceStore.CommitCount);
    }

    [Fact]
    public async Task MissingIndividualSelectionIsIndistinguishableFromAnotherInvalidDebtor()
    {
        var tenant = await CreateContextAsync();
        var orderStore = new TestOrderStore { Snapshot = CommittedOrder(tenant) };
        var invoiceStore = new InMemoryInvoiceIssueStore();
        var useCase = CreateUseCase(new TestAuthority(), invoiceStore, orderStore, new TestCustomerStore());

        var result = await useCase.ExecuteAsync(
            tenant,
            new IssueInvoiceRequest(
                orderStore.Snapshot.OrderId,
                orderStore.Snapshot.Revision,
                new InvoiceDebtorSelection.Individual(Guid.NewGuid()),
                "missing-individual"),
            CancellationToken.None);

        Assert.Equal(InvoiceIssueStatus.InvalidDebtor, result.Status);
        Assert.Equal(0, invoiceStore.CommitCount);
    }

    [Fact]
    public async Task InactiveIndividualStopsAtTheExplicitlyUnresolvedEligibilityDecision()
    {
        var tenant = await CreateContextAsync();
        var individualId = Guid.NewGuid();
        var orderStore = new TestOrderStore { Snapshot = CommittedOrder(tenant) };
        var customers = new TestCustomerStore();
        customers.Individuals[individualId] = Individual(
            tenant,
            individualId,
            "Inactive customer",
            CustomerIndividualAvailability.Inactive,
            revision: 7);
        var invoiceStore = new InMemoryInvoiceIssueStore();
        var useCase = CreateUseCase(new TestAuthority(), invoiceStore, orderStore, customers);

        var result = await useCase.ExecuteAsync(
            tenant,
            new IssueInvoiceRequest(
                orderStore.Snapshot.OrderId,
                orderStore.Snapshot.Revision,
                new InvoiceDebtorSelection.Individual(individualId),
                "inactive-individual"),
            CancellationToken.None);

        Assert.Equal(InvoiceIssueStatus.ContractDecisionRequired, result.Status);
        Assert.Equal(InvoiceIssueDecisionRequirement.InactiveIndividualEligibility, result.RequiredDecision);
        Assert.Equal(0, invoiceStore.CommitCount);
    }

    [Fact]
    public async Task ConsolidatedAwayIndividualIsNotASelectableDebtor()
    {
        var tenant = await CreateContextAsync();
        var sourceId = Guid.NewGuid();
        var survivorId = Guid.NewGuid();
        var orderStore = new TestOrderStore { Snapshot = CommittedOrder(tenant) };
        var customers = new TestCustomerStore();
        // Active and otherwise valid, but it now carries a forward redirect to the survivor.
        customers.Individuals[sourceId] = RedirectedIndividual(
            tenant, sourceId, "Pre-consolidation name", survivorId);
        var invoiceStore = new InMemoryInvoiceIssueStore();
        var useCase = CreateUseCase(new TestAuthority(), invoiceStore, orderStore, customers);

        var result = await useCase.ExecuteAsync(
            tenant,
            new IssueInvoiceRequest(
                orderStore.Snapshot.OrderId,
                orderStore.Snapshot.Revision,
                new InvoiceDebtorSelection.Individual(sourceId),
                "redirected-individual"),
            CancellationToken.None);

        // Rejected as an invalid debtor rather than frozen as issued fact: an issued invoice
        // is immutable, so accepting a merged-away identity would permanently mint a document
        // naming a customer that no longer exists, under its pre-consolidation name.
        Assert.Equal(InvoiceIssueStatus.InvalidDebtor, result.Status);
        Assert.Null(result.RequiredDecision);
        Assert.Equal(0, invoiceStore.CommitCount);
    }

    [Fact]
    public async Task SurvivorOfAConsolidationRemainsASelectableDebtor()
    {
        var tenant = await CreateContextAsync();
        var survivorId = Guid.NewGuid();
        var orderStore = new TestOrderStore { Snapshot = CommittedOrder(tenant) };
        var customers = new TestCustomerStore();
        customers.Individuals[survivorId] = Individual(
            tenant, survivorId, "Survivor", CustomerIndividualAvailability.Active, revision: 9);
        var invoiceStore = new InMemoryInvoiceIssueStore();
        var useCase = CreateUseCase(new TestAuthority(), invoiceStore, orderStore, customers);

        var result = await useCase.ExecuteAsync(
            tenant,
            new IssueInvoiceRequest(
                orderStore.Snapshot.OrderId,
                orderStore.Snapshot.Revision,
                new InvoiceDebtorSelection.Individual(survivorId),
                "survivor-individual"),
            CancellationToken.None);

        Assert.Equal(InvoiceIssueStatus.Issued, result.Status);
        Assert.Equal(1, invoiceStore.CommitCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public async Task UnsupportedIndividualAvailabilityFailsClosedBeforePersistence(int availability)
    {
        var tenant = await CreateContextAsync();
        var individualId = Guid.NewGuid();
        var orderStore = new TestOrderStore { Snapshot = CommittedOrder(tenant) };
        var customers = new TestCustomerStore();
        customers.Individuals[individualId] = Individual(
            tenant, individualId, "Synthetic customer", (CustomerIndividualAvailability)availability, revision: 1);
        var invoiceStore = new InMemoryInvoiceIssueStore();
        var useCase = CreateUseCase(new TestAuthority(), invoiceStore, orderStore, customers);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            tenant,
            new IssueInvoiceRequest(
                orderStore.Snapshot.OrderId,
                orderStore.Snapshot.Revision,
                new InvoiceDebtorSelection.Individual(individualId),
                "unsupported-availability"),
            CancellationToken.None));

        Assert.Equal("Customer individual query returned an unsupported availability.", exception.Message);
        Assert.Equal(0, invoiceStore.CommitCount);
    }

    [Fact]
    public async Task ActiveIndividualCanBeSelectedAndItsObservedFactsAreRetained()
    {
        var tenant = await CreateContextAsync();
        var individualId = Guid.NewGuid();
        var orderStore = new TestOrderStore { Snapshot = CommittedOrder(tenant) };
        var customers = new TestCustomerStore();
        customers.Individuals[individualId] = Individual(
            tenant, individualId, "Active customer", CustomerIndividualAvailability.Active, revision: 7);
        var invoiceStore = new InMemoryInvoiceIssueStore();
        var useCase = CreateUseCase(new TestAuthority(), invoiceStore, orderStore, customers);

        var result = await useCase.ExecuteAsync(
            tenant,
            new IssueInvoiceRequest(
                orderStore.Snapshot.OrderId,
                orderStore.Snapshot.Revision,
                new InvoiceDebtorSelection.Individual(individualId),
                "active-individual"),
            CancellationToken.None);

        Assert.Equal(InvoiceIssueStatus.Issued, result.Status);
        var invoice = Assert.IsType<InvoiceIssuedFacts>(result.Invoice);
        Assert.Equal(new InvoiceDebtorFacts.Individual(individualId, 7, "Active customer"), invoice.Debtor);
        Assert.Equal(1, invoiceStore.CommitCount);
    }

    [Theory]
    [InlineData(OrderDraftState.Draft, "NPR", 3, InvoiceIssueStatus.OrderNotCommitted)]
    [InlineData(OrderDraftState.Abandoned, "NPR", 3, InvoiceIssueStatus.OrderNotCommitted)]
    [InlineData(OrderDraftState.Committed, "USD", 3, InvoiceIssueStatus.UnsupportedCurrency)]
    [InlineData(OrderDraftState.Committed, "NPR", 4, InvoiceIssueStatus.OrderRevisionConflict)]
    public async Task AcceptedIssueRestrictionsFailBeforeDebtorOrPersistence(
        OrderDraftState state,
        string currencyCode,
        long expectedRevision,
        InvoiceIssueStatus expectedStatus)
    {
        var tenant = await CreateContextAsync();
        var organizationId = Guid.NewGuid();
        var snapshot = CommittedOrder(
            tenant,
            currencyCode: currencyCode,
            customerContext: new CustomerOrderContext(organizationId, null));
        snapshot = snapshot with { State = state };
        var orderStore = new TestOrderStore { Snapshot = snapshot };
        var customers = new TestCustomerStore();
        customers.Organizations[organizationId] = Organization(tenant, organizationId, "Organization");
        var invoiceStore = new InMemoryInvoiceIssueStore();
        var useCase = CreateUseCase(new TestAuthority(), invoiceStore, orderStore, customers);

        var result = await useCase.ExecuteAsync(
            tenant,
            new IssueInvoiceRequest(
                snapshot.OrderId,
                expectedRevision,
                new InvoiceDebtorSelection.DefaultOrganization(),
                $"restriction-{state}-{currencyCode}-{expectedRevision}"),
            CancellationToken.None);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(0, invoiceStore.CommitCount);
        Assert.Equal(0, customers.OrganizationFindCount);
    }

    [Fact]
    public async Task AggregateOutsideDecimal19Scale4IsRejectedBeforePersistence()
    {
        var tenant = await CreateContextAsync();
        var orderStore = new TestOrderStore
        {
            Snapshot = CommittedOrder(
                tenant,
                lines:
                [
                    new OrderDraftLine(1, "Maximum", 1m, "EA", 999_999_999_999_999.9999m,
                        999_999_999_999_999.9999m),
                    new OrderDraftLine(2, "Extra", 1m, "EA", 0.0001m, 0.0001m),
                ],
                total: 1_000_000_000_000_000.0000m),
        };
        var invoiceStore = new InMemoryInvoiceIssueStore();
        var useCase = CreateUseCase(new TestAuthority(), invoiceStore, orderStore, new TestCustomerStore());

        var result = await useCase.ExecuteAsync(
            tenant,
            new IssueInvoiceRequest(
                orderStore.Snapshot.OrderId,
                orderStore.Snapshot.Revision,
                new InvoiceDebtorSelection.Individual(Guid.NewGuid()),
                "overflow-total"),
            CancellationToken.None);

        Assert.Equal(InvoiceIssueStatus.InvalidCommittedOrderFacts, result.Status);
        Assert.Equal(0, invoiceStore.CommitCount);
    }

    [Fact]
    public async Task SourceValueWithMoreThanFourDecimalPlacesIsRejectedBeforePersistence()
    {
        var tenant = await CreateContextAsync();
        var orderStore = new TestOrderStore
        {
            Snapshot = CommittedOrder(
                tenant,
                lines: [new OrderDraftLine(1, "Bad scale", 1.00001m, "EA", 1m, 1m)],
                total: 1m),
        };
        var invoiceStore = new InMemoryInvoiceIssueStore();
        var useCase = CreateUseCase(new TestAuthority(), invoiceStore, orderStore, new TestCustomerStore());

        var result = await useCase.ExecuteAsync(
            tenant,
            new IssueInvoiceRequest(
                orderStore.Snapshot.OrderId,
                orderStore.Snapshot.Revision,
                new InvoiceDebtorSelection.Individual(Guid.NewGuid()),
                "bad-scale"),
            CancellationToken.None);

        Assert.Equal(InvoiceIssueStatus.InvalidCommittedOrderFacts, result.Status);
        Assert.Equal(0, invoiceStore.CommitCount);
    }

    [Fact]
    public async Task ExactReplayReturnsRetainedFactsWithoutReReadingChangedOrderOrCustomerState()
    {
        var tenant = await CreateContextAsync();
        var organizationId = Guid.NewGuid();
        var mutableLines = new List<OrderDraftLine>
        {
            new(1, "Original line", 1m, "EA", 10m, 10m),
        };
        var orderStore = new TestOrderStore
        {
            Snapshot = CommittedOrder(
                tenant,
                customerContext: new CustomerOrderContext(organizationId, null),
                lines: mutableLines,
                total: 10m),
        };
        var customers = new TestCustomerStore();
        customers.Organizations[organizationId] = Organization(tenant, organizationId, "Original organization");
        var invoiceStore = new InMemoryInvoiceIssueStore();
        var useCase = CreateUseCase(new TestAuthority(), invoiceStore, orderStore, customers);
        var request = new IssueInvoiceRequest(
            orderStore.Snapshot.OrderId,
            orderStore.Snapshot.Revision,
            new InvoiceDebtorSelection.DefaultOrganization(),
            "same-key");

        var issued = await useCase.ExecuteAsync(tenant, request, CancellationToken.None);
        mutableLines[0] = new OrderDraftLine(1, "Changed source", 1m, "EA", 999m, 999m);
        customers.Organizations[organizationId] = Organization(tenant, organizationId, "Changed organization");

        var replayed = await useCase.ExecuteAsync(tenant, request, CancellationToken.None);

        Assert.Equal(InvoiceIssueStatus.Issued, issued.Status);
        Assert.Equal(InvoiceIssueStatus.Replayed, replayed.Status);
        var historical = Assert.IsType<InvoiceIssuedFacts>(replayed.Invoice);
        Assert.Equal("Original line", historical.Lines[0].Description);
        Assert.Equal(10m, historical.Lines[0].UnitPrice);
        Assert.Equal("Original organization",
            Assert.IsType<InvoiceDebtorFacts.Organization>(historical.Debtor).OrganizationDisplayName);
        Assert.Equal(1, orderStore.FindCount);
        Assert.Equal(1, customers.OrganizationFindCount);
        Assert.Equal(1, invoiceStore.CommitCount);
    }

    [Fact]
    public async Task CurrentAuthorityIsRecheckedBeforeAnExistingReceiptCanBeDisclosed()
    {
        var tenant = await CreateContextAsync();
        var organizationId = Guid.NewGuid();
        var orderStore = new TestOrderStore
        {
            Snapshot = CommittedOrder(
                tenant,
                customerContext: new CustomerOrderContext(organizationId, null)),
        };
        var customers = new TestCustomerStore();
        customers.Organizations[organizationId] = Organization(tenant, organizationId, "Organization");
        var authority = new TestAuthority();
        var invoiceStore = new InMemoryInvoiceIssueStore();
        var useCase = CreateUseCase(authority, invoiceStore, orderStore, customers);
        var request = new IssueInvoiceRequest(
            orderStore.Snapshot.OrderId,
            orderStore.Snapshot.Revision,
            new InvoiceDebtorSelection.DefaultOrganization(),
            "protected-replay");
        await useCase.ExecuteAsync(tenant, request, CancellationToken.None);
        var receiptLookupsBeforeDenial = invoiceStore.ReceiptLookupCount;
        authority.Status = InvoiceIssueAuthorityStatus.Denied;

        var denied = await useCase.ExecuteAsync(tenant, request, CancellationToken.None);

        Assert.Equal(InvoiceIssueStatus.NotAuthorized, denied.Status);
        Assert.Null(denied.Invoice);
        Assert.Equal(receiptLookupsBeforeDenial, invoiceStore.ReceiptLookupCount);
        Assert.Equal(2, authority.CheckCount);
    }

    [Fact]
    public async Task ReusingAKeyForDifferentDebtorIntentIsAnIdempotencyConflict()
    {
        var tenant = await CreateContextAsync();
        var organizationId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var orderStore = new TestOrderStore
        {
            Snapshot = CommittedOrder(
                tenant,
                customerContext: new CustomerOrderContext(organizationId, programId)),
        };
        var customers = new TestCustomerStore();
        customers.Organizations[organizationId] = Organization(tenant, organizationId, "Organization");
        customers.Programs[programId] = Program(tenant, programId, organizationId, "Program");
        var invoiceStore = new InMemoryInvoiceIssueStore();
        var useCase = CreateUseCase(new TestAuthority(), invoiceStore, orderStore, customers);

        var first = await useCase.ExecuteAsync(
            tenant,
            new IssueInvoiceRequest(
                orderStore.Snapshot.OrderId,
                orderStore.Snapshot.Revision,
                new InvoiceDebtorSelection.DefaultOrganization(),
                "one-key"),
            CancellationToken.None);
        var conflict = await useCase.ExecuteAsync(
            tenant,
            new IssueInvoiceRequest(
                orderStore.Snapshot.OrderId,
                orderStore.Snapshot.Revision,
                new InvoiceDebtorSelection.AttributedProgram(),
                "one-key"),
            CancellationToken.None);

        Assert.Equal(InvoiceIssueStatus.Issued, first.Status);
        Assert.Equal(InvoiceIssueStatus.IdempotencyKeyConflict, conflict.Status);
        Assert.Equal(1, invoiceStore.CommitCount);
    }

    [Fact]
    public void IssuedFactsDefensivelyCopyLineCollections()
    {
        var lines = new List<InvoiceIssuedLine>
        {
            new(1, "Original", 1m, "EA", 1m, 1m),
        };
        var facts = new InvoiceIssuedFacts(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            null,
            "NPR",
            lines,
            1m,
            new InvoiceDebtorFacts.Individual(Guid.NewGuid(), 1, "Customer"),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero),
            new DateOnly(2026, 10, 3),
            "REF-1");

        lines[0] = new InvoiceIssuedLine(1, "Mutated", 2m, "EA", 2m, 4m);
        lines.Add(new InvoiceIssuedLine(2, "Added", 1m, "EA", 1m, 1m));

        Assert.Single(facts.Lines);
        Assert.Equal("Original", facts.Lines[0].Description);
        Assert.Equal(1m, facts.Lines[0].LineTotal);
    }

    [Fact]
    public void CapabilityRemainsHostProviderAuthorizationAndContainerNeutral()
    {
        var references = typeof(IssueInvoice).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Npgsql", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("OpenFga", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, name => name.StartsWith("Autofac", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name =>
            name.StartsWith("Application.", StringComparison.Ordinal) &&
            name is not ("Application.Orders" or "Application.Customers" or "Application.Tenancy"));
    }

    [Theory]
    [InlineData(InvoiceDebtorKind.Organization, InvoiceDebtorKind.Individual)]
    [InlineData(InvoiceDebtorKind.Program, InvoiceDebtorKind.Organization)]
    [InlineData(InvoiceDebtorKind.Individual, InvoiceDebtorKind.Program)]
    [InlineData(InvoiceDebtorKind.Organization, (InvoiceDebtorKind)99)]
    [InlineData(InvoiceDebtorKind.Program, (InvoiceDebtorKind)99)]
    [InlineData(InvoiceDebtorKind.Individual, (InvoiceDebtorKind)99)]
    public void IssuedFactsRejectDebtorKindThatContradictsItsConcreteType(
        InvoiceDebtorKind concreteKind,
        InvoiceDebtorKind retainedKind)
    {
        InvoiceDebtorFacts debtor = concreteKind switch
        {
            InvoiceDebtorKind.Organization => new InvoiceDebtorFacts.Organization(Guid.NewGuid(), "Organization"),
            InvoiceDebtorKind.Program => new InvoiceDebtorFacts.Program(
                Guid.NewGuid(), "Organization", Guid.NewGuid(), "Program"),
            InvoiceDebtorKind.Individual => new InvoiceDebtorFacts.Individual(Guid.NewGuid(), 1, "Individual"),
            _ => throw new InvalidOperationException("Unsupported test debtor kind."),
        };
        debtor = debtor with { Kind = retainedKind };

        var exception = Assert.Throws<InvoiceIssueValidationException>(() => new InvoiceIssuedFacts(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, null, "NPR",
            [new InvoiceIssuedLine(1, "Line", 1m, "EA", 1m, 1m)], 1m, debtor, Guid.NewGuid(),
            new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero), new DateOnly(2026, 10, 3), "REF-1"));

        Assert.Equal("debtor_kind_invalid", exception.Code);
    }

    [Fact]
    public void IssuedFactsAcceptTheMatchingProgramDebtorKind()
    {
        var debtor = new InvoiceDebtorFacts.Program(
            Guid.NewGuid(), "Organization", Guid.NewGuid(), "Program");

        var invoice = new InvoiceIssuedFacts(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, null, "NPR",
            [new InvoiceIssuedLine(1, "Line", 1m, "EA", 1m, 1m)], 1m, debtor, Guid.NewGuid(),
            new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero), new DateOnly(2026, 10, 3), "REF-1");

        Assert.Equal(InvoiceDebtorKind.Program, invoice.Debtor.Kind);
        Assert.Equal(debtor, invoice.Debtor);
    }

    private static IssueInvoice CreateUseCase(
        IInvoiceIssueAuthority authority,
        IInvoiceIssueStore invoiceStore,
        TestOrderStore orderStore,
        TestCustomerStore customers) =>
        new(
            authority,
            invoiceStore,
            new GetOrderDraft(orderStore),
            new GetCustomerOrganization(customers),
            new GetCustomerProgram(customers),
            new GetCustomerIndividual(customers));

    private static OrderDraftSnapshot CommittedOrder(
        TenantContext tenant,
        string currencyCode = "NPR",
        CustomerOrderContext? customerContext = null,
        IReadOnlyList<OrderDraftLine>? lines = null,
        decimal total = 10m) =>
        new(
            Guid.NewGuid(),
            tenant.TenantId,
            tenant.AccountId,
            "Committed order",
            currencyCode,
            total,
            3,
            new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero),
            lines ?? [new OrderDraftLine(1, "Line", 1m, "EA", 10m, 10m)],
            OrderDraftState.Committed,
            CustomerContext: customerContext,
            CommittedAt: new DateTimeOffset(2026, 10, 3, 10, 5, 0, TimeSpan.Zero),
            CommittedByAccountId: tenant.AccountId);

    private static CustomerOrganizationSnapshot Organization(
        TenantContext tenant,
        Guid organizationId,
        string name) =>
        new(organizationId, tenant.TenantId, name, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

    private static CustomerProgramSnapshot Program(
        TenantContext tenant,
        Guid programId,
        Guid organizationId,
        string name) =>
        new(programId, tenant.TenantId, organizationId, name,
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

    private static CustomerIndividualSnapshot Individual(
        TenantContext tenant,
        Guid individualId,
        string name,
        CustomerIndividualAvailability availability,
        long revision,
        Guid? redirectTargetIndividualId = null) =>
        new(
            individualId,
            tenant.TenantId,
            name,
            null,
            null,
            availability,
            revision,
            tenant.AccountId,
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
            null,
            null,
            RedirectTargetIndividualId: redirectTargetIndividualId);

    private static CustomerIndividualSnapshot RedirectedIndividual(
        TenantContext tenant,
        Guid individualId,
        string name,
        Guid redirectTargetIndividualId) =>
        Individual(
            tenant,
            individualId,
            name,
            CustomerIndividualAvailability.Active,
            revision: 4,
            redirectTargetIndividualId: redirectTargetIndividualId);

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

    private sealed class TestAuthority : IInvoiceIssueAuthority
    {
        public InvoiceIssueAuthorityStatus Status { get; set; } = InvoiceIssueAuthorityStatus.Allowed;
        public int CheckCount { get; private set; }

        public Task<InvoiceIssueAuthorityStatus> CheckAsync(
            TenantContext tenantContext,
            CancellationToken cancellationToken)
        {
            CheckCount++;
            return Task.FromResult(Status);
        }
    }

    private sealed class InMemoryInvoiceIssueStore : IInvoiceIssueStore
    {
        private readonly Dictionary<(Guid TenantId, Guid AccountId, string Key), StoredReceipt> receipts = [];
        private int referenceCounter;

        public int ReceiptLookupCount { get; private set; }
        public int CommitCount { get; private set; }
        public string? LastNormalizedKey { get; private set; }

        public Task<InvoiceIssueReceiptResult> FindReceiptAsync(
            TenantContext tenantContext,
            string idempotencyKey,
            string fingerprint,
            CancellationToken cancellationToken)
        {
            ReceiptLookupCount++;
            LastNormalizedKey = idempotencyKey;
            var scope = (tenantContext.TenantId, tenantContext.AccountId, idempotencyKey);
            if (!receipts.TryGetValue(scope, out var receipt))
            {
                return Task.FromResult(new InvoiceIssueReceiptResult(InvoiceIssueReceiptStatus.Missing));
            }

            return Task.FromResult(receipt.Fingerprint == fingerprint
                ? new InvoiceIssueReceiptResult(InvoiceIssueReceiptStatus.Replayed, receipt.Invoice)
                : new InvoiceIssueReceiptResult(InvoiceIssueReceiptStatus.IdempotencyKeyConflict));
        }

        public Task<InvoiceIssueCommitResult> CommitAsync(
            TenantContext tenantContext,
            InvoiceIssueCandidate candidate,
            string idempotencyKey,
            string fingerprint,
            CancellationToken cancellationToken)
        {
            CommitCount++;
            LastNormalizedKey = idempotencyKey;
            var scope = (tenantContext.TenantId, tenantContext.AccountId, idempotencyKey);
            if (receipts.TryGetValue(scope, out var existing))
            {
                return Task.FromResult(existing.Fingerprint == fingerprint
                    ? new InvoiceIssueCommitResult(InvoiceIssueCommitStatus.Replayed, existing.Invoice)
                    : new InvoiceIssueCommitResult(InvoiceIssueCommitStatus.IdempotencyKeyConflict));
            }

            var invoice = new InvoiceIssuedFacts(
                Guid.NewGuid(),
                candidate.TenantId,
                candidate.SourceOrderId,
                candidate.SourceOrderRevision,
                candidate.SourceCustomerContext,
                candidate.CurrencyCode,
                candidate.Lines,
                candidate.Total,
                candidate.Debtor,
                candidate.IssuedByAccountId,
                new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero),
                new DateOnly(2026, 10, 3),
                $"ORG-TEST-{++referenceCounter:0000}");
            receipts[scope] = new StoredReceipt(fingerprint, invoice);
            return Task.FromResult(new InvoiceIssueCommitResult(InvoiceIssueCommitStatus.Issued, invoice));
        }

        private sealed record StoredReceipt(string Fingerprint, InvoiceIssuedFacts Invoice);
    }

    private sealed class TestOrderStore : IOrderDraftStore
    {
        public required OrderDraftSnapshot Snapshot { get; set; }
        public int FindCount { get; private set; }

        public Task<OrderDraftSnapshot?> FindAsync(
            TenantContext tenantContext,
            Guid orderId,
            CancellationToken cancellationToken)
        {
            FindCount++;
            return Task.FromResult<OrderDraftSnapshot?>(
                Snapshot.TenantId == tenantContext.TenantId && Snapshot.OrderId == orderId ? Snapshot : null);
        }

        public Task<CreateOrderDraftResult> CreateAsync(
            TenantContext tenantContext,
            OrderDraftIntent intent,
            string idempotencyKey,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderDraftPage> ListAsync(
            TenantContext tenantContext,
            ListOrderDraftsRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AbandonOrderDraftResult> AbandonAsync(
            TenantContext tenantContext,
            AbandonOrderDraftRequest request,
            string idempotencyKey,
            string fingerprint,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ReviseOrderDraftResult> ReviseAsync(
            TenantContext tenantContext,
            ReviseOrderDraftRequest request,
            OrderDraftIntent intent,
            string idempotencyKey,
            string fingerprint,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CommitOrderDraftResult> CommitAsync(
            TenantContext tenantContext,
            CommitOrderDraftRequest request,
            string idempotencyKey,
            string fingerprint,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestCustomerStore : ICustomerStore, ICustomerIndividualStore
    {
        public Dictionary<Guid, CustomerOrganizationSnapshot> Organizations { get; } = [];
        public Dictionary<Guid, CustomerProgramSnapshot> Programs { get; } = [];
        public Dictionary<Guid, CustomerIndividualSnapshot> Individuals { get; } = [];
        public int OrganizationFindCount { get; private set; }

        public Task<CustomerOrganizationSnapshot?> FindOrganizationAsync(
            TenantContext tenantContext,
            Guid organizationId,
            CancellationToken cancellationToken)
        {
            OrganizationFindCount++;
            return Task.FromResult(
                Organizations.TryGetValue(organizationId, out var organization) &&
                organization.TenantId == tenantContext.TenantId
                    ? organization
                    : null);
        }

        public Task<CustomerProgramSnapshot?> FindProgramAsync(
            TenantContext tenantContext,
            Guid programId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                Programs.TryGetValue(programId, out var program) && program.TenantId == tenantContext.TenantId
                    ? program
                    : null);

        public Task<CustomerIndividualSnapshot?> FindIndividualAsync(
            TenantContext tenantContext,
            Guid individualId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                Individuals.TryGetValue(individualId, out var individual) &&
                individual.TenantId == tenantContext.TenantId
                    ? individual
                    : null);

        public Task<CustomerOrderContext?> ResolveOrderContextAsync(
            TenantContext tenantContext,
            Guid organizationId,
            Guid? programId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CreateCustomerOrganizationResult> CreateOrganizationAsync(
            TenantContext tenantContext,
            CustomerOrganizationIntent intent,
            string idempotencyKey,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CreateCustomerProgramResult> CreateProgramAsync(
            TenantContext tenantContext,
            CustomerProgramIntent intent,
            string idempotencyKey,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CustomerOrganizationPage> ListOrganizationsAsync(
            TenantContext tenantContext,
            ListCustomerOrganizationsRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CustomerProgramPage> ListProgramsAsync(
            TenantContext tenantContext,
            ListCustomerProgramsRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CreateCustomerIndividualResult> CreateIndividualAsync(
            TenantContext context,
            CustomerIndividualIntent intent,
            string idempotencyKey,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ChangeCustomerIndividualAvailabilityResult> ChangeIndividualAvailabilityAsync(
            TenantContext context,
            CustomerIndividualAvailabilityIntent intent,
            string idempotencyKey,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
