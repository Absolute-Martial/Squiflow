using Application.Invoices;
using Xunit;

namespace Application.Invoices.Tests;

public sealed class CustomerSettlementRulesTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.00001")]
    [InlineData("1.00000")]
    [InlineData("1000000000000000")]
    public void PaymentAmountsRejectZeroNegativeExcessScaleAndRange(string raw)
    {
        var amount = decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Throws<FinancialValidationException>(() => CustomerSettlementRules.Normalize(Record(amount)));
    }

    [Theory]
    [InlineData("0.0001")]
    [InlineData("1.2345")]
    [InlineData("999999999999999.9999")]
    public void ExactFourDecimalPaymentsAreRetainedWithoutRounding(string raw)
    {
        var amount = decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(amount, CustomerSettlementRules.Normalize(Record(amount)).Amount);
    }

    [Fact]
    public void BankReferencesAreTrimmedWithCasePreservedAndRequired()
    {
        var record = Record(1m) with { Channel = CustomerPaymentChannel.Bank, ExternalReference = "  Receipt-AbC  ", Evidence = "  Received in cash  " };
        var normalized = CustomerSettlementRules.Normalize(record);
        Assert.Equal("Receipt-AbC", normalized.ExternalReference);
        Assert.Equal("Received in cash", normalized.Evidence);
        Assert.Throws<FinancialValidationException>(() => CustomerSettlementRules.Normalize(record with { ExternalReference = null }));
        Assert.Throws<FinancialValidationException>(() => CustomerSettlementRules.Normalize(record with { ExternalReference = "   " }));
        Assert.Throws<FinancialValidationException>(() => CustomerSettlementRules.Normalize(record with { CurrencyCode = "USD" }));
        Assert.NotEqual(CustomerSettlementRules.Fingerprint(Guid.Parse("11111111-1111-1111-1111-111111111111"), normalized),
            CustomerSettlementRules.Fingerprint(Guid.Parse("11111111-1111-1111-1111-111111111111"), normalized with { ExternalReference = "receipt-abc" }));
    }

    [Fact]
    public void OnlyTheExactFrozenDebtorIdentityIsUsed()
    {
        var organization = Guid.NewGuid(); var program = Guid.NewGuid(); var individual = Guid.NewGuid();
        Assert.Equal(new(InvoiceDebtorKind.Organization, organization), FinancialDebtor.From(new InvoiceDebtorFacts.Organization(organization, "Organization")));
        Assert.Equal(new(InvoiceDebtorKind.Program, program), FinancialDebtor.From(new InvoiceDebtorFacts.Program(organization, "Organization", program, "Program")));
        Assert.Equal(new(InvoiceDebtorKind.Individual, individual), FinancialDebtor.From(new InvoiceDebtorFacts.Individual(individual, 2, "Individual")));
        Assert.Throws<FinancialValidationException>(() => CustomerSettlementRules.Normalize(Record(1m) with { Debtor = new(InvoiceDebtorKind.Organization, Guid.Empty) }));
        Assert.Throws<FinancialValidationException>(() => CustomerSettlementRules.Normalize(Record(1m) with { Debtor = new((InvoiceDebtorKind)9, organization) }));
    }

    [Fact]
    public void RecorderCannotConfirmEvenWithIndependentReconciliationAuthority()
    {
        var payment = Payment(CustomerPaymentState.Pending);
        var mutation = new CustomerPaymentMutation(CustomerPaymentOperation.Confirm, 1, "Actual funds checked");
        Assert.Equal(FinancialStatus.SelfConfirmation, CustomerSettlementRules.CanApply(payment, mutation, payment.RecordedByAccountId));
        Assert.Equal(FinancialStatus.Applied, CustomerSettlementRules.CanApply(payment, mutation, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(CustomerPaymentState.Pending, CustomerPaymentOperation.Confirm, FinancialStatus.Applied)]
    [InlineData(CustomerPaymentState.Pending, CustomerPaymentOperation.Reject, FinancialStatus.Applied)]
    [InlineData(CustomerPaymentState.Pending, CustomerPaymentOperation.Dispute, FinancialStatus.Applied)]
    [InlineData(CustomerPaymentState.Confirmed, CustomerPaymentOperation.Dispute, FinancialStatus.Applied)]
    [InlineData(CustomerPaymentState.Disputed, CustomerPaymentOperation.Confirm, FinancialStatus.Applied)]
    [InlineData(CustomerPaymentState.Confirmed, CustomerPaymentOperation.Confirm, FinancialStatus.InvalidTransition)]
    [InlineData(CustomerPaymentState.Disputed, CustomerPaymentOperation.Reject, FinancialStatus.InvalidTransition)]
    [InlineData(CustomerPaymentState.Rejected, CustomerPaymentOperation.Confirm, FinancialStatus.InvalidTransition)]
    [InlineData(CustomerPaymentState.Reversed, CustomerPaymentOperation.Confirm, FinancialStatus.InvalidTransition)]
    public void PaymentTransitionsFollowTheAcceptedManualEvidenceLifecycle(CustomerPaymentState state, CustomerPaymentOperation operation, FinancialStatus expected)
    {
        var payment = Payment(state);
        Assert.Equal(expected, CustomerSettlementRules.CanApply(payment, new(operation, 1, "New evidence"), Guid.NewGuid()));
    }

    [Fact]
    public void DisputePreservesPriorAllocationsAndRequiresNewConfirmationEvidence()
    {
        var payment = Payment(CustomerPaymentState.Disputed) with { AllocatedAmount = 4m, LastConfirmationEvidence = "Original confirmation" };
        Assert.Equal(0m, payment.AvailableAmount);
        Assert.Equal(FinancialStatus.InvalidTransition, CustomerSettlementRules.CanApply(payment,
            new(CustomerPaymentOperation.Confirm, 1, "Original confirmation"), Guid.NewGuid()));
        Assert.Equal(FinancialStatus.Applied, CustomerSettlementRules.CanApply(payment,
            new(CustomerPaymentOperation.Confirm, 1, "New evidence"), Guid.NewGuid()));
        Assert.Equal(4m, payment.AllocatedAmount);
        Assert.Equal(FinancialStatus.InvalidTransition, CustomerSettlementRules.CanApply(payment,
            new(CustomerPaymentOperation.Allocate, 1, ReceivableId: Guid.NewGuid(), Amount: 1m), Guid.NewGuid()));
    }

    [Fact]
    public void FullPaymentReversalRequiresAllAllocationsToBeReleased()
    {
        var payment = Payment(CustomerPaymentState.Confirmed) with { AllocatedAmount = 4m };
        var reverse = new CustomerPaymentMutation(CustomerPaymentOperation.Reverse, 1, "Recorded the wrong amount");
        Assert.Equal(FinancialStatus.ActiveAllocations, CustomerSettlementRules.CanApply(payment, reverse, Guid.NewGuid()));
        Assert.Equal(FinancialStatus.Applied, CustomerSettlementRules.CanApply(payment with { AllocatedAmount = 0m }, reverse, Guid.NewGuid()));
    }

    [Fact]
    public void AllocationsCannotUsePendingOrUnavailableFunds()
    {
        var payment = Payment(CustomerPaymentState.Pending);
        var allocation = new CustomerPaymentMutation(CustomerPaymentOperation.Allocate, 1, ReceivableId: Guid.NewGuid(), Amount: 1m);
        Assert.Equal(0m, payment.AvailableAmount);
        Assert.Equal(FinancialStatus.InvalidTransition, CustomerSettlementRules.CanApply(payment, allocation, Guid.NewGuid()));
        payment = payment with { State = CustomerPaymentState.Confirmed, AllocatedAmount = 9.5m };
        Assert.Equal(0.5m, payment.AvailableAmount);
        Assert.Equal(FinancialStatus.InsufficientFunds, CustomerSettlementRules.CanApply(payment, allocation, Guid.NewGuid()));
        Assert.Equal(FinancialStatus.Applied, CustomerSettlementRules.CanApply(payment, allocation with { Amount = 0.5m }, Guid.NewGuid()));
    }

    [Fact]
    public void ReleaseCannotAcceptPartialAmountsOrChangedTargets()
    {
        var release = new CustomerPaymentMutation(CustomerPaymentOperation.Release, 1, "Correct the allocation", AllocationId: Guid.NewGuid());
        Assert.Equal(release, CustomerSettlementRules.Normalize(release));
        Assert.Throws<FinancialValidationException>(() => CustomerSettlementRules.Normalize(release with { Amount = 1m }));
        Assert.Throws<FinancialValidationException>(() => CustomerSettlementRules.Normalize(release with { ReceivableId = Guid.NewGuid() }));
        Assert.Throws<FinancialValidationException>(() => CustomerSettlementRules.Normalize(release with { AllocationId = null }));
        Assert.Throws<FinancialValidationException>(() => CustomerSettlementRules.Normalize(release with { Evidence = "\nprivate" }));
    }

    [Fact]
    public void FinancialCapabilitysRemainSeparateAndCorrectionIsExplicit()
    {
        Assert.Equal(FinancialCapability.Record, CustomerSettlementRules.Permission(CustomerPaymentOperation.Record));
        Assert.Equal(FinancialCapability.Reconcile, CustomerSettlementRules.Permission(CustomerPaymentOperation.Confirm));
        Assert.Equal(FinancialCapability.Allocate, CustomerSettlementRules.Permission(CustomerPaymentOperation.Allocate));
        Assert.Equal(FinancialCapability.Correct, CustomerSettlementRules.Permission(CustomerPaymentOperation.Release));
        Assert.Equal(FinancialCapability.Correct, CustomerSettlementRules.Permission(CustomerPaymentOperation.Reverse));
    }

    [Fact]
    public void SemanticFingerprintsPreserveNormalizedIntentWithoutDelimiterCollisions()
    {
        var id = Guid.NewGuid();
        var first = CustomerSettlementRules.Normalize(Record(1.0000m) with { Evidence = "  received |1:x  " });
        var second = CustomerSettlementRules.Normalize(first with { Amount = 1m });
        Assert.Equal(CustomerSettlementRules.Fingerprint(id, first), CustomerSettlementRules.Fingerprint(id, second));
        Assert.NotEqual(CustomerSettlementRules.Fingerprint(id, first), CustomerSettlementRules.Fingerprint(id, first with { Evidence = "received" }));
        Assert.Throws<FinancialValidationException>(() => CustomerSettlementRules.Key("contains a space"));
        Assert.Throws<FinancialValidationException>(() => CustomerSettlementRules.Key(new string('a', 129)));
    }

    [Fact]
    public void ZeroInvoiceIsSettledWithoutAnyFabricatedPayment()
    {
        var receivable = new ReceivableSnapshot(Guid.NewGuid(), Guid.NewGuid(), new(InvoiceDebtorKind.Organization, Guid.NewGuid()),
            "NPR", 0m, new(2026, 10, 15), DateTimeOffset.UtcNow, 0m);
        Assert.True(receivable.IsSettled);
        Assert.Equal(0m, receivable.OutstandingAmount);
    }

    [Fact]
    public void ResourceBoundsReserveRoomForFullAllocationReleaseAndPaymentReversal()
    {
        var payment = Payment(CustomerPaymentState.Confirmed) with { Revision = CustomerSettlementRules.MaximumOrdinaryEventsPerPayment };
        Assert.Equal(FinancialStatus.ResourceLimit, CustomerSettlementRules.CanApply(payment,
            new(CustomerPaymentOperation.Allocate, payment.Revision, ReceivableId: Guid.NewGuid(), Amount: 1m), Guid.NewGuid()));
        Assert.Equal(FinancialStatus.Applied, CustomerSettlementRules.CanApply(payment,
            new(CustomerPaymentOperation.Release, payment.Revision, "Release original allocation", AllocationId: Guid.NewGuid()), Guid.NewGuid()));
        payment = payment with { Revision = CustomerSettlementRules.MaximumEventsPerPayment - 1 };
        Assert.Equal(FinancialStatus.Applied, CustomerSettlementRules.CanApply(payment,
            new(CustomerPaymentOperation.Reverse, payment.Revision, "Correct receipt evidence"), Guid.NewGuid()));
    }

    private static CustomerPaymentRecord Record(decimal amount) => new(new(InvoiceDebtorKind.Organization, Guid.NewGuid()), "NPR", amount,
        CustomerPaymentChannel.Cash, "Received in cash");
    private static CustomerPaymentSnapshot Payment(CustomerPaymentState state) => new(Guid.NewGuid(), Guid.NewGuid(),
        new(InvoiceDebtorKind.Organization, Guid.NewGuid()), "NPR", 10m, CustomerPaymentChannel.Cash, "Receipt note", null,
        Guid.NewGuid(), DateTimeOffset.UtcNow, 1, state, 0m, null);
}
