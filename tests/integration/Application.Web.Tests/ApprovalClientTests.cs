using System.Net;
using System.Text;
using System.Text.Json;
using Application.Web.Business;
using Xunit;

namespace Application.Web.Tests;

public sealed partial class BusinessClientTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(6, false)]
    public async Task ApprovalWireEnumsRemainNumericAndUnknownValuesFailClosed(int state, bool valid)
    {
        var body = $$"""
            {"requestId":"30000000-0000-0000-0000-000000000001","orderId":"30000000-0000-0000-0000-000000000002","orderRevision":1,"requestRevision":2,"state":{{state}}}
            """;
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));
        using var http = new HttpClient(handler); var (api, _, _, _) = await ClientAsync(http);
        var result = await api.GetAsync<ApprovalReceipt>(Tenant, "order-approvals/30000000-0000-0000-0000-000000000001", row => ApprovalContracts.Receipt(row));
        Assert.Equal(valid, result.Value is not null);
    }

    [Theory]
    [InlineData("\"Pending\"")]
    [InlineData("null")]
    public async Task ApprovalStateCannotBeInferredFromAStringOrNull(string state)
    {
        var body = $$"""
            {"requestId":"30000000-0000-0000-0000-000000000001","orderId":"30000000-0000-0000-0000-000000000002","orderRevision":1,"requestRevision":2,"state":{{state}}}
            """;
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));
        using var http = new HttpClient(handler); var (api, _, _, _) = await ClientAsync(http);
        Assert.Equal(BusinessFailure.InvalidResponse, (await api.GetAsync<ApprovalReceipt>(Tenant, "order-approvals/30000000-0000-0000-0000-000000000001", row => ApprovalContracts.Receipt(row))).Failure);
    }

    [Fact]
    public void UnicodeReasonAndReferenceBoundsCountScalarsAndRejectMalformedText()
    {
        var nonBmp = char.ConvertFromUtf32(0x1F600);
        Assert.True(ApprovalContracts.ScalarText(string.Concat(Enumerable.Repeat(nonBmp, 128)), 128));
        Assert.False(ApprovalContracts.ScalarText(string.Concat(Enumerable.Repeat(nonBmp, 129)), 128));
        Assert.False(ApprovalContracts.ScalarText("reason\n", 512));
        Assert.False(ApprovalContracts.ScalarText("\ud800", 512));
        Assert.False(ApprovalContracts.ScalarText("  ", 512));
        Assert.True(ApprovalContracts.ScalarText(null, 128, false));
    }

    [Fact]
    public void ApprovalSubjectsMustMatchTheRequestAndSupportedPinnedShape()
    {
        var order = new ApprovalOrder(IssuedId, "Order", "NPR", 25, 1, Organization.CreatedAt,
            [new(1, "Print", 2, "EA", 12.5m, 25)], "draft", null, null, null, null, new(Guid.NewGuid(), Guid.NewGuid(), false, true), null);
        var subject = new ApprovalSubject(1, order, Guid.NewGuid(), new string('a', 64));
        var detail = new ApprovalDetail(QuoteId, order.OrderId, Guid.NewGuid(), Guid.NewGuid(), 1, ApprovalState.Pending,
            Organization.CreatedAt, Organization.CreatedAt, subject, Guid.NewGuid(), 1, ApprovalChange.Submitted, null);
        Assert.True(ApprovalContracts.Detail(detail, QuoteId));
        Assert.False(ApprovalContracts.Detail(detail with { OrderId = Guid.NewGuid() }));
        Assert.False(ApprovalContracts.Detail(detail with { Subject = subject with { SchemaVersion = 2 } }));
        Assert.False(ApprovalContracts.Detail(detail with { Subject = subject with { Fingerprint = "unsafe" } }));
        Assert.False(ApprovalContracts.Detail(detail with { Subject = subject with { Order = order with { ProgramPolicy = null } } }));
    }
}
