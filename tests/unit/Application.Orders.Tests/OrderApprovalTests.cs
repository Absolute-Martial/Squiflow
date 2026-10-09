using Application.Customers;
using Xunit;

namespace Application.Orders.Tests;

public sealed class OrderApprovalTests
{
    [Fact]
    public void ApprovalSubjectBindsPricesReferencePolicyAndExactRevision()
    {
        var order = Draft();
        var subject = OrderApprovalRules.CreateSubject(order, order.CreatedByAccountId);
        Assert.True(OrderApprovalRules.Matches(subject, order));
        Assert.False(OrderApprovalRules.Matches(subject, order with { Revision = 2 }));
        Assert.False(OrderApprovalRules.Matches(subject, order with { ExternalProgramReference = "Different" }));
        Assert.False(OrderApprovalRules.Matches(subject, order with { ProgramPolicy = order.ProgramPolicy! with { PolicyRevisionId = Guid.NewGuid() } }));
        Assert.False(OrderApprovalRules.Matches(subject, order with { Lines = [order.Lines[0] with { UnitPrice = 20, LineTotal = 40 }], Total = 40 }));
        var request = Request(subject);
        Assert.False(OrderApprovalRules.IsIndependent(request, request.RequestedByAccountId));
        Assert.False(OrderApprovalRules.IsIndependent(request, subject.RevisionAuthorAccountId));
        Assert.True(OrderApprovalRules.IsIndependent(request, request.AssignedAccountId));
    }

    [Fact]
    public void ExactSubjectComparisonUsesDecimalValuesAcrossPersistenceRepresentations()
    {
        var order = Draft();
        var subject = OrderApprovalRules.CreateSubject(order, order.CreatedByAccountId);
        var persisted = order with
        {
            Total = decimal.Parse(order.Total.ToString("F8", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture),
            Lines = order.Lines.Select(line => line with
            {
                Quantity = decimal.Parse(line.Quantity.ToString("F8", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture),
                UnitPrice = decimal.Parse(line.UnitPrice.ToString("F8", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture),
                LineTotal = decimal.Parse(line.LineTotal.ToString("F8", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture),
            }).ToArray(),
        };
        Assert.True(OrderApprovalRules.Matches(subject, persisted));
        Assert.False(OrderApprovalRules.Matches(subject, persisted with { Lines = [persisted.Lines[0] with { UnitPrice = persisted.Lines[0].UnitPrice + 0.00000001m }] }));
    }

    [Fact]
    public void ApprovalRequirementIsProgramAttributionAndPinnedFlagOnly()
    {
        var required = Draft();
        Assert.True(OrderApprovalRules.IsRequired(required));
        Assert.False(OrderApprovalRules.IsRequired(required with { ProgramPolicy = required.ProgramPolicy! with { RequireApprovalForProgramOrders = false } }));
        Assert.False(OrderApprovalRules.IsRequired(required with { CustomerContext = null }));
        Assert.False(OrderApprovalRules.IsRequired(required with { CustomerContext = new(required.CustomerContext!.OrganizationId, null) }));
    }

    [Theory]
    [InlineData("\nvalid")]
    [InlineData("valid\t")]
    [InlineData("valid\u007f")]
    [InlineData("   ")]
    public void ReasonsRejectControlsMalformedUtf16AndEmptyText(string reason) =>
        Assert.Throws<OrderDraftValidationException>(() => OrderApprovalRules.NormalizeReason(reason, true));

    [Fact]
    public void ReasonsRejectMalformedUtf16ConstructedAtTheCallBoundary()
    {
        foreach (var codeUnit in new[] { (char)0xD800, (char)0xDC00 })
            Assert.Throws<OrderDraftValidationException>(() => OrderApprovalRules.NormalizeReason(new string(codeUnit, 1), true));
    }

    [Fact]
    public void ReasonsPreserveCaseAndCountUnicodeScalarsAfterOuterTrim()
    {
        var reason = string.Concat(Enumerable.Repeat("😀", 512));
        Assert.Equal(reason, OrderApprovalRules.NormalizeReason($"  {reason}  ", true));
        Assert.Equal("Review PO", OrderApprovalRules.NormalizeReason("  Review PO  ", true));
        Assert.Throws<OrderDraftValidationException>(() => OrderApprovalRules.NormalizeReason(reason + "😀", true));
    }

    [Fact]
    public void CorruptedSubjectAuthorityAndArithmeticFailClosed()
    {
        var order = Draft();
        var request = Request(OrderApprovalRules.CreateSubject(order, order.CreatedByAccountId));
        OrderApprovalRules.RequireValidStored(request);
        Assert.Throws<InvalidOperationException>(() => OrderApprovalRules.RequireValidStored(request with { AuthorizationRevision = 0 }));
        Assert.Throws<InvalidOperationException>(() => OrderApprovalRules.RequireValidStored(request with { AssignedAccountId = order.CreatedByAccountId }));
        Assert.Throws<InvalidOperationException>(() => OrderApprovalRules.RequireValidStored(request with { Subject = request.Subject with { Fingerprint = new string('0', 64) } }));
        Assert.Throws<InvalidOperationException>(() => OrderApprovalRules.CreateSubject(order with { Total = 10 }, order.CreatedByAccountId));
        Assert.Throws<InvalidOperationException>(() => OrderApprovalRules.CreateSubject(order, Guid.Empty));
    }

    private static OrderDraftSnapshot Draft() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Program work", "NPR", 20, 1,
        DateTimeOffset.UnixEpoch, [new(1, "Work", 2, "EA", 10, 20)],
        CustomerContext: new CustomerOrderContext(Guid.NewGuid(), Guid.NewGuid()),
        ProgramPolicy: new(Guid.NewGuid(), Guid.NewGuid(), true, true), ExternalProgramReference: "PO-1");

    private static OrderApprovalSnapshot Request(OrderApprovalSubject subject)
    {
        var requester = Guid.NewGuid();
        return new(Guid.NewGuid(), subject.Order.TenantId, subject.Order.OrderId, requester, Guid.NewGuid(), 1,
            OrderApprovalState.Pending, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, subject, requester, 2,
            OrderApprovalChange.Submitted, null);
    }
}
