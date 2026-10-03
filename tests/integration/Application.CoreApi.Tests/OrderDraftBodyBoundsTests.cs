using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class OrderDraftBodyBoundsTests : IClassFixture<WhiteLabelApiFactory>
{
    private readonly WhiteLabelApiFactory _factory;

    public OrderDraftBodyBoundsTests(WhiteLabelApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(false, 60000, true, "utf-8")]
    [InlineData(true, 60000, true, "utf-8")]
    [InlineData(false, 65536, true, "utf-8")]
    [InlineData(true, 65536, true, "utf-8")]
    [InlineData(false, 65537, true, "utf-8")]
    [InlineData(true, 65537, true, "utf-8")]
    [InlineData(false, 65536, false, "utf-8")]
    [InlineData(true, 65536, false, "utf-8")]
    [InlineData(false, 65537, false, "utf-8")]
    [InlineData(true, 65537, false, "utf-8")]
    [InlineData(false, 60000, true, "utf-16")]
    [InlineData(true, 60000, true, "utf-16")]
    [InlineData(false, 60000, true, "unsupported-test-charset")]
    [InlineData(true, 60000, true, "unsupported-test-charset")]
    public async Task CreateAndRevisionEnforceActualStreamBoundsWithoutContentLength(bool revise, int bytes, bool useKestrel, string charset)
    {
        var probe = new BodyLengthProbe(clearBufferedContentLength: !useKestrel);
        using var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IStartupFilter>(probe)));
        // TestServer buffers HttpContent and fills Content-Length. Exercise the real
        // HTTP/1.1 chunked transport so a declared-length check cannot mask this boundary.
        if (useKestrel)
            host.UseKestrel(0);
        using var client = host.CreateClient();
        var account = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var subject = account.ToString("D");
        _factory.Bind(subject, account);
        _factory.AddTenantMembership(account, tenant, "Bounded draft tenant");
        _factory.SetOrderCreateDecision(account, tenant, allowed: true);
        _factory.SetOrderEditDecision(account, tenant, allowed: true);
        _factory.SetOrderManualPriceDecision(account, tenant, true);
        var token = _factory.CreateToken(subject);
        var originalBody = """
            {"summary":"Bounded draft","currencyCode":"USD","lines":[{"description":"Line","quantity":1,"unitCode":"EA","unitPrice":10}]}
            """;
        Guid orderId = Guid.Empty;
        if (revise)
        {
            using var seed = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tenants/{tenant:D}/orders");
            seed.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            seed.Headers.Add("Idempotency-Key", "seed");
            seed.Content = new StringContent(originalBody, Encoding.UTF8, "application/json");
            using var created = await client.SendAsync(seed);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            using var order = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            orderId = order.RootElement.GetProperty("orderId").GetGuid();
        }
        var body = revise ? originalBody.Insert(1, "\"expectedRevision\":1,") : originalBody;
        using var request = new HttpRequestMessage(revise ? HttpMethod.Put : HttpMethod.Post,
            revise ? $"/api/v1/tenants/{tenant:D}/orders/{orderId:D}/draft" : $"/api/v1/tenants/{tenant:D}/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", "bounded-command");
        request.Headers.TransferEncodingChunked = true;
        var utf16 = charset == "utf-16";
        var invalidCharset = charset == "unsupported-test-charset";
        var encoding = utf16 ? Encoding.Unicode : Encoding.UTF8;
        var encodedBody = encoding.GetBytes(body.PadRight(utf16 ? bytes / 2 : bytes, ' '));
        Assert.Equal(bytes, encodedBody.Length);
        request.Content = new UnknownLengthJsonContent(encodedBody, charset);
        Assert.Null(request.Content.Headers.ContentLength);
        using var response = await client.SendAsync(request);
        Assert.Null(probe.ContentLength);
        // Kestrel's HTTP/1.1 limit also counts chunk framing. Keep its transport
        // protection and separately prove the decoded-byte ceiling without that feature.
        var rejected = useKestrel ? bytes >= 65536 : bytes > 65536;
        Assert.Equal(invalidCharset ? HttpStatusCode.BadRequest : !rejected ? (revise ? HttpStatusCode.OK : HttpStatusCode.Created) : HttpStatusCode.RequestEntityTooLarge,
            response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        if (rejected || invalidCharset)
        {
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(invalidCharset ? "request_invalid" : "request_too_large", problem.RootElement.GetProperty("code").GetString());
            Assert.DoesNotContain("unsupported-test-charset", problem.RootElement.GetRawText());
            Assert.DoesNotContain("Exception", problem.RootElement.GetRawText());
            Assert.Equal(revise ? 1 : 0, _factory.GetOrderCreateCount(tenant));
            Assert.Equal(0, _factory.GetOrderReviseCount(tenant));
        }
    }

    private sealed class UnknownLengthJsonContent : HttpContent
    {
        private readonly byte[] _body;

        public UnknownLengthJsonContent(byte[] body, string charset)
        {
            _body = body;
            Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = charset };
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(_body).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class BodyLengthProbe(bool clearBufferedContentLength) : IStartupFilter
    {
        public long? ContentLength { get; private set; } = long.MinValue;

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, continuation) =>
            {
                if (clearBufferedContentLength)
                    context.Request.ContentLength = null;
                ContentLength = context.Request.ContentLength;
                return continuation(context);
            });
            next(app);
        };
    }
}
