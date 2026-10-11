using Application.Purchasing;
using Xunit;

namespace Application.Purchasing.Tests;

public sealed class PurchasingRulesTests
{
    private static readonly Guid Tenant = Id(1);
    private static readonly Guid SupplierId = Id(2);
    private static readonly Guid UnitId = Id(3);
    private static readonly Guid PurchaseId = Id(4);
    private static readonly Guid Actor = Id(5);
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly PurchasingSourceFacts Source = new(new(Tenant, UnitId, "each", 2, 3, true));

    [Fact]
    public void SupplierIdentityRevisionsAndRetirementPreserveHistoricalPurchases()
    {
        var created = PurchasingRules.ApplySupplier(Tenant, null, new(SupplierCommandKind.Create, 0, "  Printer  ", "  Phone  "),
            SupplierId, Id(100), Actor, 7, Now);
        var supplier = created.Snapshot!;
        Assert.Equal("Printer", supplier.Name);
        Assert.Equal("Phone", supplier.Contact);
        Assert.True(PurchasingRules.IsValidSupplierEvent(created.Event));
        var purchase = Request(supplier);
        var retired = PurchasingRules.ApplySupplier(Tenant, supplier, new(SupplierCommandKind.Retire, 1),
            SupplierId, Id(101), Actor, 7, Now.AddSeconds(1));
        Assert.Equal(SupplierStatus.Retired, retired.Snapshot!.Status);
        Assert.Equal(PurchasingMutationStatus.SupplierRetired, RequestResult(retired.Snapshot).Status);
        var edited = PurchasingRules.ApplySupplier(Tenant, retired.Snapshot, new(SupplierCommandKind.Revise, 2, "Other name", "Contact"),
            SupplierId, Id(102), Actor, 7, Now.AddSeconds(2));
        Assert.Equal(SupplierStatus.Retired, edited.Snapshot!.Status);
        Assert.Equal(SupplierStatus.Active, purchase.Supplier.Status);
        Assert.Equal("Printer", purchase.Supplier.Name);
        Assert.Equal(PurchasingMutationStatus.Applied,
            Apply(purchase, new(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: 10m)).Status);
        Assert.Equal(PurchasingMutationStatus.RevisionConflict,
            PurchasingRules.ApplySupplier(Tenant, supplier, new(SupplierCommandKind.Revise, 0, "Printer"),
                SupplierId, Id(103), Actor, 7, Now).Status);
    }

    [Fact]
    public void InformalRequestFreezesItsUnitAndOptionalCommittedOrderReference()
    {
        var purchase = Request();
        Assert.Null(purchase.Order);
        Assert.Equal(Source.Unit, purchase.Unit);
        Assert.Equal(3m, purchase.RequestedQuantity);
        Assert.Equal(0m, purchase.OutstandingPayable);
        Assert.True(PurchasingRules.IsValidSnapshot(purchase));
        var link = new PurchasingOrderLink(Id(20), 2, 5);
        var source = Source with { Order = new(Tenant, link.OrderId, 2, 5, true) };
        Assert.True(PurchasingRules.IsEligibleSource(source, Tenant, UnitId, link));
        Assert.False(PurchasingRules.IsEligibleSource(source, Tenant, UnitId, null));
        Assert.False(PurchasingRules.IsEligibleSource(source with { Order = source.Order! with { TenantId = Id(30) } }, Tenant, UnitId, link));
        Assert.False(PurchasingRules.IsEligibleSource(source with { Order = source.Order! with { IsCommitted = false } }, Tenant, UnitId, link));
        Assert.False(PurchasingRules.IsEligibleSource(source, Tenant, UnitId, link with { OrderRevision = 6 }));
        Assert.False(PurchasingRules.IsEligibleSource(source, Tenant, Id(30), link));
    }

    [Fact]
    public void UntrustedSourceShapesCannotIntroduceCrossTenantRetiredOrMalformedUnits()
    {
        var invalid = new[]
        {
            Source.Unit with { TenantId = Id(99) }, Source.Unit with { UnitId = Guid.Empty },
            Source.Unit with { UnitCode = " each " }, Source.Unit with { UnitCode = "bad\nunit" },
            Source.Unit with { UnitPrecision = -1 }, Source.Unit with { UnitPrecision = 5 },
            Source.Unit with { UnitRevision = 0 }, Source.Unit with { UnitRevision = long.MaxValue },
            Source.Unit with { IsActive = false },
        };
        Assert.All(invalid, unit => Assert.False(PurchasingRules.IsEligibleSource(new(unit), Tenant, UnitId, null)));
        var linked = Source with { Order = new(Tenant, Id(30), 1, 1, true) };
        Assert.False(PurchasingRules.IsEligibleSource(linked, Tenant, UnitId, new(Id(30), 0, 1)));
        Assert.False(PurchasingRules.IsEligibleSource(linked, Tenant, UnitId, new(Id(30), 101, 1)));
        Assert.False(PurchasingRules.IsEligibleSource(linked, Tenant, UnitId, new(Id(30), 1, 0)));
    }

    [Theory]
    [InlineData(0, "1", true)]
    [InlineData(0, "1.1", false)]
    [InlineData(2, "1.23", true)]
    [InlineData(2, "1.234", false)]
    [InlineData(4, "0.0001", true)]
    [InlineData(4, "0.00001", false)]
    [InlineData(5, "1", false)]
    [InlineData(2, "0", false)]
    public void QuantityPrecisionIsExactWithoutRounding(int precision, string number, bool expected)
    {
        var quantity = decimal.Parse(number, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(expected, PurchasingRules.IsQuantity(quantity, precision));
    }

    [Fact]
    public void CostsPaymentsAndRefundsArePositiveExactNprDecimal19Scale2()
    {
        Assert.True(PurchasingRules.IsAmount(0.01m));
        Assert.True(PurchasingRules.IsAmount(PurchasingRules.MaximumAmount));
        Assert.False(PurchasingRules.IsAmount(0m));
        Assert.False(PurchasingRules.IsAmount(-1m));
        Assert.False(PurchasingRules.IsAmount(0.001m));
        Assert.False(PurchasingRules.IsAmount(PurchasingRules.MaximumAmount + 0.01m));
        var purchase = Request();
        Assert.Equal(PurchasingMutationStatus.AmountInvalid,
            Apply(purchase, new(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: 10.001m)).Status);
        Assert.Equal(PurchasingMutationStatus.QuantityInvalid,
            Apply(purchase, new(PurchasingCommandKind.Receive, 1, Quantity: 0.001m, Amount: 10m)).Status);
        Assert.Equal(PurchasingMutationStatus.OverReceipt,
            Apply(purchase, new(PurchasingCommandKind.Receive, 1, Quantity: 3.01m, Amount: 10m)).Status);
    }

    [Fact]
    public void PartialReceiptsPayablesPaymentsAndActualRefundsHaveIndependentBalances()
    {
        var receipt = Apply(Request(), new(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: 120m));
        var purchase = receipt.Snapshot!;
        var receiptId = receipt.Event!.EventId;
        Assert.Equal(120m, purchase.OutstandingPayable);
        var paid = Apply(purchase, new(PurchasingCommandKind.Pay, 2, Amount: 70m, ReceiptId: receiptId, Method: SupplierPaymentMethod.Cash));
        purchase = paid.Snapshot!;
        Assert.Equal(50m, purchase.OutstandingPayable);
        var paymentId = paid.Event!.EventId;
        Assert.Equal(PurchasingMutationStatus.OverPayment,
            Apply(purchase, new(PurchasingCommandKind.Pay, 3, Amount: 50.01m, ReceiptId: receiptId, Method: SupplierPaymentMethod.Bank)).Status);
        Assert.Equal(PurchasingMutationStatus.DependencyInvalid,
            Apply(purchase, new(PurchasingCommandKind.Refund, 3, Amount: 1m, PaymentId: receiptId, Method: SupplierPaymentMethod.Cash)).Status);
        Assert.Equal(PurchasingMutationStatus.OverRefund,
            Apply(purchase, new(PurchasingCommandKind.Refund, 3, Amount: 70.01m, PaymentId: paymentId, Method: SupplierPaymentMethod.Cash)).Status);
        purchase = Apply(purchase, new(PurchasingCommandKind.Refund, 3, Amount: 20m, PaymentId: paymentId,
            Method: SupplierPaymentMethod.Bank, Reference: "  transfer  ")).Snapshot!;
        Assert.Equal("transfer", purchase.Refunds[0].Reference);
        Assert.Equal(70m, purchase.OutstandingPayable);
        purchase = Apply(purchase, new(PurchasingCommandKind.Receive, 4, Quantity: 0.5m, Amount: 40m)).Snapshot!;
        Assert.Equal(1.5m, purchase.ReceivedQuantity);
        Assert.Equal(160m, purchase.TotalCost);
        Assert.Equal(70m, purchase.PaidAmount);
        Assert.Equal(20m, purchase.RefundedAmount);
        Assert.Equal(110m, purchase.OutstandingPayable);
        Assert.True(PurchasingRules.IsValidSnapshot(purchase));
    }

    [Fact]
    public void ReceiptCorrectionRequiresActualRefundBeforeCostFallsBelowNetPaid()
    {
        var received = Apply(Request(), new(PurchasingCommandKind.Receive, 1, Quantity: 2m, Amount: 100m));
        var paid = Apply(received.Snapshot!, new(PurchasingCommandKind.Pay, 2, Amount: 80m,
            ReceiptId: received.Event!.EventId, Method: SupplierPaymentMethod.Bank));
        var correction = new PurchasingMutation(PurchasingCommandKind.Correct, 3, Reason: "Wrong recorded cost",
            TargetEventId: received.Event.EventId, Replacement: new(PurchasingEventKind.Receipt, 1.5m, 60m));
        Assert.Equal(PurchasingMutationStatus.CorrectionHasDependents, Apply(paid.Snapshot!, correction).Status);
        var refunded = Apply(paid.Snapshot!, new(PurchasingCommandKind.Refund, 3, Amount: 20m,
            PaymentId: paid.Event!.EventId, Method: SupplierPaymentMethod.Bank));
        var corrected = Apply(refunded.Snapshot!, correction with { ExpectedRevision = 4 });
        Assert.Equal(PurchasingMutationStatus.Applied, corrected.Status);
        Assert.Equal(0m, corrected.Snapshot!.OutstandingPayable);
        Assert.Equal(1.5m, corrected.Snapshot.ReceivedQuantity);
        Assert.Equal(100m, received.Event.Facts!.Amount);
        Assert.Equal(received.Event.EventId, corrected.Event!.TargetEventId);
        Assert.Equal(received.Event.EventId, corrected.Snapshot.Receipts[0].EntryId);
        Assert.Equal(corrected.Event.EventId, corrected.Snapshot.Receipts[0].EffectiveEventId);
        Assert.Equal(PurchasingMutationStatus.CorrectionTargetInvalid,
            Apply(corrected.Snapshot, correction with { ExpectedRevision = 5 }).Status);
        Assert.Equal(PurchasingMutationStatus.CorrectionHasDependents,
            Apply(corrected.Snapshot, new(PurchasingCommandKind.Correct, 5, Reason: "Remove refund",
                TargetEventId: refunded.Event!.EventId)).Status);
    }

    [Fact]
    public void CorrectionsKeepPaymentRefundDependenciesAndOriginalFacts()
    {
        var receipt = Apply(Request(), new(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: 100m));
        var paid = Apply(receipt.Snapshot!, new(PurchasingCommandKind.Pay, 2, Amount: 80m,
            ReceiptId: receipt.Event!.EventId, Method: SupplierPaymentMethod.Cash));
        var refunded = Apply(paid.Snapshot!, new(PurchasingCommandKind.Refund, 3, Amount: 30m,
            PaymentId: paid.Event!.EventId, Method: SupplierPaymentMethod.Cash));
        var correction = new PurchasingMutation(PurchasingCommandKind.Correct, 4, Reason: "Wrong payment",
            TargetEventId: paid.Event.EventId, Replacement: new(PurchasingEventKind.Payment, Amount: 20m,
                ReceiptId: receipt.Event.EventId, Method: SupplierPaymentMethod.Cash));
        Assert.Equal(PurchasingMutationStatus.CorrectionHasDependents, Apply(refunded.Snapshot!, correction).Status);
        Assert.Equal(PurchasingMutationStatus.CorrectionHasDependents, Apply(refunded.Snapshot!, correction with
        { Replacement = correction.Replacement! with { Amount = 70m, ReceiptId = Id(99) } }).Status);
        var corrected = Apply(refunded.Snapshot!, correction with { Replacement = correction.Replacement! with { Amount = 70m } });
        Assert.Equal(PurchasingMutationStatus.Applied, corrected.Status);
        Assert.Equal(80m, paid.Event.Facts!.Amount);
        Assert.Equal(60m, corrected.Snapshot!.OutstandingPayable);
        Assert.Equal(paid.Event.EventId, corrected.Snapshot.Refunds[0].PaymentId);
        Assert.Equal(PurchasingMutationStatus.CorrectionHasDependents, Apply(corrected.Snapshot,
            new(PurchasingCommandKind.Correct, 5, Reason: "Void receipt", TargetEventId: receipt.Event.EventId)).Status);
    }

    [Fact]
    public void CorrectionVoidsRetainStableEntriesAndRepeatedCorrectionsTargetCurrentReplacement()
    {
        var receipt = Apply(Request(), new(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: 50m));
        var corrected = Apply(receipt.Snapshot!, new(PurchasingCommandKind.Correct, 2, Reason: "Wrong amount",
            TargetEventId: receipt.Event!.EventId, Replacement: new(PurchasingEventKind.Receipt, 0.5m, 25m)));
        var voided = Apply(corrected.Snapshot!, new(PurchasingCommandKind.Correct, 3, Reason: "Receipt did not occur",
            TargetEventId: corrected.Event!.EventId));
        Assert.Equal(PurchasingMutationStatus.Applied, voided.Status);
        Assert.True(voided.Snapshot!.Receipts[0].IsVoided);
        Assert.Equal(receipt.Event.EventId, voided.Snapshot.Receipts[0].EntryId);
        Assert.Equal(0m, voided.Snapshot.ReceivedQuantity);
        Assert.Equal(0m, voided.Snapshot.OutstandingPayable);
        Assert.Equal(50m, receipt.Event.Facts!.Amount);
        Assert.Equal(25m, corrected.Event.Replacement!.Amount);
        Assert.Equal(PurchasingMutationStatus.CorrectionTargetInvalid,
            Apply(voided.Snapshot, new(PurchasingCommandKind.Correct, 4, Reason: "Void again", TargetEventId: voided.Event!.EventId)).Status);
        Assert.True(PurchasingRules.IsValidSnapshot(voided.Snapshot));
    }

    [Fact]
    public void CancellationPreservesReceivedCostAndCancelsOnlyOriginalUnreceivedRemainder()
    {
        var received = Apply(Request(), new(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: 50m));
        var cancelled = Apply(received.Snapshot!, new(PurchasingCommandKind.Cancel, 2, Reason: "  No longer needed  "));
        var purchase = cancelled.Snapshot!;
        Assert.Equal(2m, purchase.CancelledQuantity);
        Assert.Equal(50m, purchase.OutstandingPayable);
        Assert.Equal("No longer needed", cancelled.Event!.Reason);
        Assert.True(purchase.IsCancelled);
        Assert.Equal(0m, purchase.RemainingQuantity);
        Assert.Equal(PurchasingMutationStatus.RequestCancelled,
            Apply(purchase, new(PurchasingCommandKind.Receive, 3, Quantity: 1m, Amount: 50m)).Status);
        var corrected = Apply(purchase, new(PurchasingCommandKind.Correct, 3, Reason: "Quantity entry wrong",
            TargetEventId: received.Event!.EventId, Replacement: new(PurchasingEventKind.Receipt, 0.5m, 50m)));
        Assert.Equal(2m, corrected.Snapshot!.CancelledQuantity);
        Assert.Equal(0.5m, corrected.Snapshot.UnfulfilledQuantity);
        Assert.True(corrected.Snapshot.IsCancelled);
        Assert.Equal(PurchasingMutationStatus.CorrectionHasDependents,
            Apply(purchase, new(PurchasingCommandKind.Correct, 3, Reason: "Quantity entry wrong",
                TargetEventId: received.Event.EventId, Replacement: new(PurchasingEventKind.Receipt, 1.5m, 50m))).Status);
        Assert.Equal(PurchasingMutationStatus.Applied,
            Apply(purchase, new(PurchasingCommandKind.Pay, 3, Amount: 50m, ReceiptId: received.Event.EventId,
                Method: SupplierPaymentMethod.Cash)).Status);
    }

    [Fact]
    public void SixRightsAreIndependentAndCorrectionNeedsBothOwnerAndTrustedRecentAuthentication()
    {
        var none = new PurchasingAuthority(1, false, false, false, false, false, false, true, true);
        Assert.False(PurchasingRules.CanExecuteSupplier(none));
        Assert.All(Enum.GetValues<PurchasingCommandKind>(), kind => Assert.False(PurchasingRules.CanExecute(none, kind)));
        var request = none with { CanRecordRequest = true };
        Assert.True(PurchasingRules.CanExecute(request, PurchasingCommandKind.Request));
        Assert.True(PurchasingRules.CanExecute(request, PurchasingCommandKind.Cancel));
        Assert.False(PurchasingRules.CanExecute(request, PurchasingCommandKind.Receive));
        Assert.False(PurchasingRules.CanExecute(request, PurchasingCommandKind.Pay));
        var correct = none with { CanCorrect = true };
        Assert.True(PurchasingRules.CanExecute(correct, PurchasingCommandKind.Correct));
        Assert.False(PurchasingRules.CanExecute(correct with { IsCurrentInitialOwner = false }, PurchasingCommandKind.Correct));
        Assert.False(PurchasingRules.CanExecute(correct with { HasRecentAuthentication = false }, PurchasingCommandKind.Correct));
        Assert.False(PurchasingRules.CanExecute(correct with { AuthorizationRevision = 0 }, PurchasingCommandKind.Correct));
        Assert.False(PurchasingRules.CanExecute(correct, PurchasingCommandKind.Refund));
    }

    [Fact]
    public void SemanticFingerprintNormalizesOuterWhitespaceOptionalBlankAndDecimalScaleOnly()
    {
        var first = new PurchasingMutation(PurchasingCommandKind.Pay, 4, Amount: 10.00m, ReceiptId: Id(30),
            Method: SupplierPaymentMethod.Bank, Reference: "   transfer  ");
        var normalized = first with { Amount = 10m, Reference = "transfer" };
        Assert.Equal(PurchasingRules.Fingerprint(PurchaseId, first), PurchasingRules.Fingerprint(PurchaseId, normalized));
        Assert.NotEqual(PurchasingRules.Fingerprint(PurchaseId, first), PurchasingRules.Fingerprint(PurchaseId, first with { Amount = 10.01m }));
        Assert.Equal(PurchasingRules.Fingerprint(SupplierId, new SupplierMutation(SupplierCommandKind.Create, 0, " Printer ", "  ")),
            PurchasingRules.Fingerprint(SupplierId, new SupplierMutation(SupplierCommandKind.Create, 0, "Printer")));
        Assert.Equal("key", PurchasingRules.NormalizeIdempotencyKey(" key "));
        Assert.Throws<PurchasingValidationException>(() => PurchasingRules.NormalizeIdempotencyKey("bad\tkey"));
    }

    [Fact]
    public void TextValidationRejectsControlsMalformedUtf16AndOversizeBeforeTrimming()
    {
        foreach (var name in new[] { "\nPrinter", "\uD800", new string('x', 129), " " })
        {
            var result = PurchasingRules.ApplySupplier(Tenant, null, new(SupplierCommandKind.Create, 0, name), SupplierId, Id(100), Actor, 1, Now);
            Assert.Equal(PurchasingMutationStatus.TextInvalid, result.Status);
        }
        var emojiName = string.Concat(Enumerable.Repeat("😀", 128));
        Assert.Equal(PurchasingMutationStatus.Applied,
            PurchasingRules.ApplySupplier(Tenant, null, new(SupplierCommandKind.Create, 0, emojiName), SupplierId, Id(100), Actor, 1, Now).Status);
        Assert.Throws<PurchasingValidationException>(() => PurchasingRules.CanonicalMutation(
            new SupplierMutation(SupplierCommandKind.Create, 0, "Printer", new string(' ', 9000))));
        Assert.Equal(PurchasingMutationStatus.InputInvalid,
            Apply(Request(), new(PurchasingCommandKind.Cancel, 1, Reason: "Reason", Amount: 1m)).Status);
    }

    [Fact]
    public void PersistedVersionSourceAndFinancialCorruptionFailClosed()
    {
        var received = Apply(Request(), new(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: 10m));
        var purchase = received.Snapshot!;
        Assert.True(PurchasingRules.IsValidEvent(received.Event, purchase));
        Assert.False(PurchasingRules.IsValidSnapshot(purchase with { SchemaVersion = 2 }));
        Assert.False(PurchasingRules.IsValidSnapshot(purchase with { Supplier = purchase.Supplier with { SchemaVersion = 2 } }));
        Assert.False(PurchasingRules.IsValidSnapshot(purchase with { Unit = purchase.Unit with { UnitPrecision = 5 } }));
        Assert.False(PurchasingRules.IsValidEvent(received.Event! with { SchemaVersion = 2 }, purchase));
        Assert.False(PurchasingRules.IsValidSnapshot(purchase with
        {
            Payments = new[] { new PurchasingPayment(Id(50), Id(50), Id(99), 1m, SupplierPaymentMethod.Cash) },
            Revision = 3,
        }));
        Assert.Equal(PurchasingMutationStatus.InvalidProjection,
            Apply(purchase with { TenantId = Id(99) }, new(PurchasingCommandKind.Cancel, 2, Reason: "Reason")).Status);
        Assert.Equal(PurchasingMutationStatus.RevisionConflict,
            Apply(purchase, new(PurchasingCommandKind.Cancel, 1, Reason: "Reason")).Status);
    }

    [Fact]
    public void FactResourceLimitRejectsWithoutPartialEffect()
    {
        var purchase = Request();
        for (var i = 0; i < PurchasingRules.MaximumFacts; i++)
        {
            var result = Apply(purchase, new(PurchasingCommandKind.Receive, purchase.Revision, Quantity: 0.01m, Amount: 0.01m));
            Assert.Equal(PurchasingMutationStatus.Applied, result.Status);
            purchase = result.Snapshot!;
        }
        Assert.True(PurchasingRules.IsValidSnapshot(purchase));
        var rejected = Apply(purchase, new(PurchasingCommandKind.Receive, purchase.Revision, Quantity: 0.01m, Amount: 0.01m));
        Assert.Equal(PurchasingMutationStatus.ResourceLimit, rejected.Status);
        Assert.Null(rejected.Snapshot);
        Assert.Null(rejected.Event);
        Assert.Equal(PurchasingRules.MaximumFacts, purchase.Receipts.Count);
    }

    [Fact]
    public void SnapshotByteLimitRejectsAValidBoundedFactWithoutPartialEffect()
    {
        var received = Apply(Request(), new(PurchasingCommandKind.Receive, 1, Quantity: 1m, Amount: 1000m));
        var purchase = received.Snapshot!;
        var reference = string.Concat(Enumerable.Repeat("😀", PurchasingRules.MaximumReferenceScalars));
        PurchasingTransitionResult? rejected = null;
        for (var i = 0; i < PurchasingRules.MaximumFacts; i++)
        {
            var result = Apply(purchase, new(PurchasingCommandKind.Pay, purchase.Revision, Amount: 1m,
                ReceiptId: received.Event!.EventId, Method: SupplierPaymentMethod.Bank, Reference: reference));
            if (result.Status == PurchasingMutationStatus.ResourceLimit)
            {
                rejected = result;
                break;
            }
            Assert.Equal(PurchasingMutationStatus.Applied, result.Status);
            purchase = result.Snapshot!;
        }
        Assert.NotNull(rejected);
        Assert.Null(rejected.Snapshot);
        Assert.Null(rejected.Event);
        Assert.True(purchase.Payments.Count + purchase.Receipts.Count < PurchasingRules.MaximumFacts);
        Assert.True(PurchasingRules.IsValidSnapshot(purchase));
    }

    [Fact]
    public void PurchasingKeepsHostProviderAndOtherCommercialInternalsOutsideItsBoundary()
    {
        var names = typeof(PurchasingRules).Assembly.GetReferencedAssemblies().Select(x => x.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(names, x => x.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(names, x => x.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(names, x => x.StartsWith("Npgsql", StringComparison.Ordinal));
        Assert.DoesNotContain(names, x => x.StartsWith("OpenFga", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, x => x.StartsWith("Autofac", StringComparison.Ordinal));
        Assert.DoesNotContain(names, x => x.StartsWith("Application.", StringComparison.Ordinal) && x != "Application.Tenancy");
    }

    private static SupplierSnapshot Supplier() => new(SupplierId, Tenant, 1, "Printer", null, SupplierStatus.Active,
        Actor, Now, Actor, Now);
    private static PurchaseSnapshot Request(SupplierSnapshot? supplier = null) => RequestResult(supplier ?? Supplier()).Snapshot!;
    private static PurchasingTransitionResult RequestResult(SupplierSnapshot supplier) => PurchasingRules.ApplyPurchase(Tenant, null,
        new(PurchasingCommandKind.Request, 0, SupplierId, UnitId, Description: "Printing", Quantity: 3m), supplier, Source,
        PurchaseId, Id(100), Actor, 1, supplier.UpdatedAt);
    private static PurchasingTransitionResult Apply(PurchaseSnapshot current, PurchasingMutation mutation) =>
        PurchasingRules.ApplyPurchase(Tenant, current, mutation, null, null, PurchaseId, Id(checked(100 + (int)current.Revision)),
            Actor, 1, Now.AddSeconds(current.Revision));
    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}
