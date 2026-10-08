using Application.Customers;
using Application.Orders;
using Xunit;

namespace Application.Orders.Tests;

public sealed class OrderProgramReferenceTests
{
    [Fact]
    public void NormalizeTrimsOuterWhitespaceAndPreservesCaseAndInternalWhitespace()
    {
        Assert.Equal("Po  42/Job", OrderProgramReference.Normalize("  Po  42/Job  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \u2003 ")]
    public void NormalizeReturnsNullForMissingText(string? input)
    {
        Assert.Null(OrderProgramReference.Normalize(input));
    }

    [Fact]
    public void NormalizeBoundsUnicodeScalarValues()
    {
        var maximum = string.Concat(Enumerable.Repeat("😀", OrderProgramReference.MaximumLength));

        Assert.Equal(maximum, OrderProgramReference.Normalize(maximum));
        Assert.Throws<ArgumentException>(() =>
            OrderProgramReference.Normalize(maximum + "😀"));
    }

    [Theory]
    [InlineData("A\u0000B")]
    [InlineData("A\u0085B")]
    [InlineData("A\tB")]
    public void NormalizeRejectsControlsAndMalformedUtf16(string input)
    {
        var exception = Assert.Throws<ArgumentException>(() => OrderProgramReference.Normalize(input));

        Assert.Equal("input", exception.ParamName);
        Assert.DoesNotContain(input, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeRejectsMalformedUtf16ConstructedWithoutAttributeSerialization()
    {
        foreach (var surrogate in new[] { '\uD800', '\uDC00' })
        {
            var input = new string(['A', surrogate, 'B']);
            var exception = Assert.Throws<ArgumentException>(() => OrderProgramReference.Normalize(input));
            Assert.DoesNotContain(input, exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RequireValidStoredRejectsEmptyPolicyIdsAndNoncanonicalText()
    {
        var policy = new OrderProgramPolicyFacts(Guid.NewGuid(), Guid.NewGuid(), true);

        Assert.Throws<InvalidOperationException>(() =>
            OrderProgramReference.RequireValidStored(policy with { ProfileId = Guid.Empty }, "PO-1"));
        Assert.Throws<InvalidOperationException>(() =>
            OrderProgramReference.RequireValidStored(policy with { PolicyRevisionId = Guid.Empty }, "PO-1"));
        Assert.Throws<InvalidOperationException>(() =>
            OrderProgramReference.RequireValidStored(policy, " PO-1 "));
        OrderProgramReference.RequireValidStored(policy, "PO-1");
        OrderProgramReference.RequireValidStored(policy, null);
    }

    [Fact]
    public void MissingReferenceRequiresPinnedRequiredPolicyAndProgramAttribution()
    {
        var policy = new OrderProgramPolicyFacts(Guid.NewGuid(), Guid.NewGuid(), true);
        var order = CreateOrder(programId: Guid.NewGuid()) with
        {
            ProgramPolicy = policy,
        };

        Assert.True(OrderProgramReference.IsMissing(order));
        Assert.False(OrderProgramReference.IsMissing(order with
        {
            ProgramPolicy = policy with { RequireReferenceForProgramOrders = false },
        }));
        Assert.False(OrderProgramReference.IsMissing(order with
        {
            CustomerContext = new CustomerOrderContext(Guid.NewGuid(), null),
        }));
        Assert.False(OrderProgramReference.IsMissing(order with
        {
            ExternalProgramReference = "PO-1",
        }));
        Assert.False(OrderProgramReference.IsMissing(order with
        {
            ProgramPolicy = null,
        }));
    }

    private static OrderDraftSnapshot CreateOrder(Guid? programId) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Order",
        "NPR",
        0m,
        1,
        DateTimeOffset.UnixEpoch,
        [],
        CustomerContext: new CustomerOrderContext(Guid.NewGuid(), programId));
}
