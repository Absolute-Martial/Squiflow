using System.Net;
using System.Net.Http.Json;
using Application.Web.Business;
using Xunit;

namespace Application.Web.Tests;

public sealed partial class BusinessClientTests
{
    [Fact]
    public async Task SuppliedPricePreviewUsesCurrentCredentialsWithoutAMutationKeyOrSavedOrder()
    {
        var input = new OrderPriceInput("NPR", [new("Printed sheets", 2m, "EA", 12.5m)]);
        var preview = new OrderPricePreview("NPR", [new(1, "Printed sheets", 2m, "EA", 12.5m, 25m)], 25m);
        using var handler = new Handler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal($"/api/v1/tenants/{Tenant:D}/orders/price-preview", request.RequestUri!.AbsolutePath);
            Assert.False(request.Headers.Contains("Idempotency-Key"));
            Assert.Equal("private-session-token", request.Headers.Authorization?.Parameter);
            Assert.Equal(input.CurrencyCode, (await request.Content!.ReadFromJsonAsync<OrderPriceInput>(ct))!.CurrencyCode);
            return Json(preview);
        });
        using var http = new HttpClient(handler);
        var (api, _, _, _) = await ClientAsync(http);
        var result = await api.QueryAsync<OrderPricePreview>(Tenant, "orders/price-preview", input, value => OrderContracts.Preview(value, input));
        Assert.Equal(preview.Total, result.Value!.Total);
        Assert.False(result.Unknown);
        Assert.False(result.Accepted);
    }

    [Fact]
    public async Task PreviewRejectsMalformedOrMismatchedCalculationFacts()
    {
        var input = new OrderPriceInput("NPR", [new("Printed sheets", 2m, "EA", 12.5m)]);
        var good = new OrderPricePreview("NPR", [new(1, "Printed sheets", 2m, "EA", 12.5m, 25m)], 25m);
        Assert.True(OrderContracts.PriceInput(input));
        Assert.False(OrderContracts.PriceInput(input with { Lines = [input.Lines[0] with { Quantity = 0 }] }));
        var invalid = new[]
        {
            good with { CurrencyCode = "USD" }, good with { Lines = [] }, good with { Total = -1 },
            good with { Lines = [good.Lines[0] with { Position = 2 }] },
            good with { Lines = [good.Lines[0] with { Quantity = 3 }] },
            good with { Lines = [good.Lines[0] with { UnitPrice = 13 }] },
            good with { Lines = [good.Lines[0] with { LineTotal = -1 }] },
        };
        foreach (var value in invalid)
        {
            using var handler = new Handler((_, _) => Task.FromResult(Json(value)));
            using var http = new HttpClient(handler);
            var (api, _, _, _) = await ClientAsync(http);
            var result = await api.QueryAsync<OrderPricePreview>(Tenant, "orders/price-preview", input, row => OrderContracts.Preview(row, input));
            Assert.Null(result.Value); Assert.Equal(BusinessFailure.InvalidResponse, result.Failure); Assert.False(result.Unknown);
        }
    }
}

public sealed partial class BusinessPageTests
{
    [Fact]
    public async Task OrderBillingContinuationKeepsSessionAndWorkspaceAdmission()
    {
        await using var factory = new BusinessFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new("https://web.example.test"), AllowAutoRedirect = false });
        var route = $"/workspace/{factory.Protocol.FirstTenant:D}/billing/orders/{Guid.NewGuid():D}";
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(route)).StatusCode);
        Assert.False((await client.PostAsync(route, JsonContent.Create(new { expectedOrderRevision = 1 }))).IsSuccessStatusCode);
        await LoginAsync(factory, client);
        var html = await client.GetStringAsync(route);
        Assert.Contains("<h1>Billing</h1>", html, StringComparison.Ordinal);
        Assert.Contains("Load committed Order for billing", html, StringComparison.Ordinal);
        Assert.DoesNotContain(Application.Entry.TestSupport.EntryProtocolFixture.AccessToken, html, StringComparison.Ordinal);
        factory.Protocol.DenyWorkspace = true;
        var denied = await client.GetStringAsync(route);
        Assert.Contains("Workspace unavailable", denied, StringComparison.Ordinal);
        Assert.DoesNotContain("Load committed Order for billing", denied, StringComparison.Ordinal);
    }
}
