using SquiFlow.Payments.Domain;
using Xunit;

namespace SquiFlow.Payments.Tests.Domain;

public sealed class PaymentStatusTests
{
    [Fact]
    public void Accepted_statuses_match_the_documented_payment_semantics()
    {
        var statuses = Enum.GetValues<PaymentStatus>();

        Assert.Equal(8, statuses.Length);
        Assert.Contains(PaymentStatus.NotStarted, statuses);
        Assert.Contains(PaymentStatus.Pending, statuses);
        Assert.Contains(PaymentStatus.Succeeded, statuses);
        Assert.Contains(PaymentStatus.Failed, statuses);
        Assert.Contains(PaymentStatus.OutcomeUnknown, statuses);
        Assert.Contains(PaymentStatus.PartiallyRefunded, statuses);
        Assert.Contains(PaymentStatus.Refunded, statuses);
        Assert.Contains(PaymentStatus.Reversed, statuses);
    }
}
