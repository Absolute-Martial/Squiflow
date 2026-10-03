using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class MutationDiagnosticsTests
{
    private const string PrivateContent = "private-payload-canary";
    private const string PrivateKey = "private-key-canary";
    private static readonly string[] OutcomeLabelNames = ["operation", "outcome"];
    private static readonly string[] FailureLabelNames = ["channel"];

    [Fact]
    public async Task AllCurrentCommandsDistinguishCommittedAndReplayedOutcomesWithoutPayloads()
    {
        using var baseline = new WhiteLabelApiFactory();
        var logs = new CapturedLogs();
        using var factory = baseline.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddLogging(logging => logging.AddProvider(logs))));
        using var metrics = new CapturedMetrics(factory.Services.GetRequiredService<IMeterFactory>());
        using var client = factory.CreateClient();
        var (account, tenant, token) = ConfigureAccess(baseline, client);
        client.DefaultRequestHeaders.Add("traceparent", "00-cccccccccccccccccccccccccccccccc-dddddddddddddddd-01");
        var organizations = $"/api/v1/tenants/{tenant:D}/customers/organizations";
        var name = new { displayName = PrivateContent };
        var organization = await SendAsync(client, HttpMethod.Post, organizations, name, PrivateKey + "-org", HttpStatusCode.Created);
        var organizationId = organization.GetProperty("organizationId").GetGuid();
        await SendAsync(client, HttpMethod.Post, organizations, name, PrivateKey + "-org", HttpStatusCode.OK);
        var programs = $"{organizations}/{organizationId:D}/programs";
        var program = await SendAsync(client, HttpMethod.Post, programs, name, PrivateKey + "-program", HttpStatusCode.Created);
        var programId = program.GetProperty("programId").GetGuid();
        await SendAsync(client, HttpMethod.Post, programs, name, PrivateKey + "-program", HttpStatusCode.OK);

        var orders = $"/api/v1/tenants/{tenant:D}/orders";
        var order = await SendAsync(client, HttpMethod.Post, orders, DraftPayload(), PrivateKey + "-order", HttpStatusCode.Created);
        var orderId = order.GetProperty("orderId").GetGuid();
        await SendAsync(client, HttpMethod.Post, orders, DraftPayload(), PrivateKey + "-order", HttpStatusCode.OK);
        var revision = new
        {
            expectedRevision = 1,
            summary = PrivateContent,
            currencyCode = "USD",
            lines = new[] { new { description = PrivateContent, quantity = 2m, unitCode = "ea", unitPrice = 7m } },
        };
        await SendAsync(client, HttpMethod.Put, $"{orders}/{orderId:D}/draft", revision, PrivateKey + "-edit", HttpStatusCode.OK);
        await SendAsync(client, HttpMethod.Put, $"{orders}/{orderId:D}/draft", revision, PrivateKey + "-edit", HttpStatusCode.OK);
        var abandonment = new { expectedRevision = 2 };
        await SendAsync(client, HttpMethod.Post, $"{orders}/{orderId:D}/abandon", abandonment, PrivateKey + "-abandon", HttpStatusCode.OK);
        await SendAsync(client, HttpMethod.Post, $"{orders}/{orderId:D}/abandon", abandonment, PrivateKey + "-abandon", HttpStatusCode.OK);

        Assert.Equal(10, logs.Events.Count);
        Assert.Equal(10, metrics.Outcomes.Count());
        var expectedResources = new Dictionary<string, Guid>
        {
            ["customer.organization.create"] = organizationId,
            ["customer.program.create"] = programId,
            ["order.draft.create"] = orderId,
            ["order.draft.revise"] = orderId,
            ["order.draft.abandon"] = orderId,
        };
        foreach (var (operation, resource) in expectedResources)
        {
            var events = logs.Events.Where(entry => (string)entry["Operation"]! == operation).ToArray();
            Assert.Equal(2, events.Length);
            Assert.Single(events, entry => Equals(entry["Replayed"], false));
            Assert.Single(events, entry => Equals(entry["Replayed"], true));
            Assert.All(events, entry =>
            {
                Assert.Equal(tenant, entry["TenantId"]);
                Assert.Equal(account, entry["AccountId"]);
                Assert.Equal(resource, entry["ResourceId"]);
                Assert.False(string.IsNullOrWhiteSpace((string?)entry["RequestId"]));
                Assert.DoesNotContain("cccccccccccccccccccccccccccccccc", (string)entry["RequestId"]!);
            });
            var measurements = metrics.Outcomes.Where(entry => Equals(entry.Tags["operation"], operation)).ToArray();
            Assert.Equal(2, measurements.Length);
            Assert.Single(measurements, entry => Equals(entry.Tags["outcome"], "committed"));
            Assert.Single(measurements, entry => Equals(entry.Tags["outcome"], "replayed"));
            Assert.All(measurements, entry =>
            {
                Assert.Equal(1, entry.Value);
                Assert.Equal(OutcomeLabelNames, entry.Tags.Keys.Order().ToArray());
            });
        }
        Assert.All(logs.Messages, message =>
        {
            Assert.DoesNotContain(PrivateContent, message);
            Assert.DoesNotContain(PrivateKey, message);
            Assert.DoesNotContain(token, message);
        });
    }

    [Fact]
    public async Task ParallelHostInstancesHaveIndependentMetricScopes()
    {
        using var first = new WhiteLabelApiFactory();
        using var second = new WhiteLabelApiFactory();
        using var firstMetrics = new CapturedMetrics(first.Services.GetRequiredService<IMeterFactory>());
        using var secondMetrics = new CapturedMetrics(second.Services.GetRequiredService<IMeterFactory>());
        using var firstClient = first.CreateClient();
        using var secondClient = second.CreateClient();
        var (_, firstTenant, _) = ConfigureAccess(first, firstClient);
        var (_, secondTenant, _) = ConfigureAccess(second, secondClient);

        await SendAsync(firstClient, HttpMethod.Post, $"/api/v1/tenants/{firstTenant:D}/orders",
            DraftPayload(), PrivateKey, HttpStatusCode.Created);
        Assert.Single(firstMetrics.Outcomes);
        Assert.Empty(secondMetrics.Outcomes);
        await SendAsync(secondClient, HttpMethod.Post, $"/api/v1/tenants/{secondTenant:D}/orders",
            DraftPayload(), PrivateKey, HttpStatusCode.Created);
        Assert.Single(firstMetrics.Outcomes);
        Assert.Single(secondMetrics.Outcomes);
    }

    [Fact]
    public async Task RejectionsAndQueriesDoNotEmitSuccessfulMutationOutcomes()
    {
        using var baseline = new WhiteLabelApiFactory();
        var logs = new CapturedLogs();
        using var factory = baseline.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddLogging(logging => logging.AddProvider(logs))));
        using var metrics = new CapturedMetrics(factory.Services.GetRequiredService<IMeterFactory>());
        using var client = factory.CreateClient();
        var (account, tenant, _) = ConfigureAccess(baseline, client);
        var organizations = $"/api/v1/tenants/{tenant:D}/customers/organizations";
        await SendAsync(client, HttpMethod.Post, organizations, new { displayName = PrivateContent }, PrivateKey, HttpStatusCode.Created);
        await SendAsync(client, HttpMethod.Post, organizations, new { displayName = "changed" }, PrivateKey, HttpStatusCode.Conflict);
        await SendAsync(client, HttpMethod.Post, organizations, new { displayName = "" }, "invalid", HttpStatusCode.BadRequest);
        await SendAsync(client, HttpMethod.Post, $"{organizations}/{Guid.NewGuid():D}/programs",
            new { displayName = PrivateContent }, "missing-parent", HttpStatusCode.NotFound);
        using var browse = await client.GetAsync(organizations);
        Assert.Equal(HttpStatusCode.OK, browse.StatusCode);
        baseline.SetCustomerDecision(account, tenant, "createOrganization", false);
        await SendAsync(client, HttpMethod.Post, organizations, new { displayName = PrivateContent }, "denied", HttpStatusCode.Forbidden);
        baseline.SetCustomerUnavailable(account, tenant, "createOrganization");
        await SendAsync(client, HttpMethod.Post, organizations, new { displayName = PrivateContent }, "outage", HttpStatusCode.ServiceUnavailable);

        var orders = $"/api/v1/tenants/{tenant:D}/orders";
        var draft = await SendAsync(client, HttpMethod.Post, orders, DraftPayload(), "draft", HttpStatusCode.Created);
        var orderId = draft.GetProperty("orderId").GetGuid();
        await SendAsync(client, HttpMethod.Post, $"{orders}/{orderId:D}/abandon", new { expectedRevision = 99 }, "stale", HttpStatusCode.Conflict);
        await SendAsync(client, HttpMethod.Post, $"{orders}/{Guid.NewGuid():D}/abandon", new { expectedRevision = 1 }, "missing", HttpStatusCode.NotFound);
        baseline.SetOrderCreateDecision(account, tenant, false);
        await SendAsync(client, HttpMethod.Post, orders, DraftPayload(), "forbidden", HttpStatusCode.Forbidden);
        Assert.Equal(2, logs.Events.Count);
        Assert.Equal(2, metrics.Outcomes.Count());
        Assert.All(logs.Events, entry => Assert.Equal(false, entry["Replayed"]));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public async Task SinkFailuresCannotTurnCommittedCommandsOrReplaysIntoErrors(
        bool failLogging, bool failMetrics, bool failFailureCounter)
    {
        using var baseline = new WhiteLabelApiFactory();
        var logs = new CapturedLogs { FailMutations = failLogging };
        using var factory = baseline.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddLogging(logging => logging.AddProvider(logs))));
        using var metrics = new CapturedMetrics(factory.Services.GetRequiredService<IMeterFactory>(), failMetrics, failFailureCounter);
        using var client = factory.CreateClient();
        var (_, tenant, _) = ConfigureAccess(baseline, client);
        var path = $"/api/v1/tenants/{tenant:D}/orders";
        var created = await SendAsync(client, HttpMethod.Post, path, DraftPayload(), PrivateKey, HttpStatusCode.Created);
        var replay = await SendAsync(client, HttpMethod.Post, path, DraftPayload(), PrivateKey, HttpStatusCode.OK);
        Assert.Equal(created.GetProperty("orderId").GetGuid(), replay.GetProperty("orderId").GetGuid());
        Assert.Equal(1, baseline.GetOrderCreateCount(tenant));
        Assert.Equal(failLogging ? 0 : 2, logs.Events.Count);
        Assert.Equal(failMetrics ? 0 : 2, metrics.Outcomes.Count());
        if (!failFailureCounter)
        {
            var failures = metrics.Measurements.Where(entry => entry.Name.EndsWith("diagnostic_failures", StringComparison.Ordinal)).ToArray();
            Assert.Equal(2, failures.Length);
            Assert.All(failures, entry =>
            {
                Assert.Equal(1, entry.Value);
                Assert.Equal(FailureLabelNames, entry.Tags.Keys.ToArray());
                Assert.Equal(failLogging ? "logging" : "metrics", entry.Tags["channel"]);
            });
        }
    }

    private static (Guid Account, Guid Tenant, string Token) ConfigureAccess(WhiteLabelApiFactory baseline, HttpClient client)
    {
        var account = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        const string subject = "mutation-diagnostics-operator";
        baseline.Bind(subject, account);
        baseline.AddTenantMembership(account, tenant, "Diagnostics tenant");
        foreach (var operation in new[] { "createOrganization", "createProgram", "viewOrganizations", "viewPrograms" })
            baseline.SetCustomerDecision(account, tenant, operation, true);
        baseline.SetOrderCreateDecision(account, tenant, true);
        baseline.SetOrderEditDecision(account, tenant, true);
        baseline.SetOrderManualPriceDecision(account, tenant, true);
        baseline.SetOrderAbandonDecision(account, tenant, true);
        var token = baseline.CreateToken(subject);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (account, tenant, token);
    }

    private static object DraftPayload() => new
    {
        summary = PrivateContent,
        currencyCode = "USD",
        lines = new[] { new { description = PrivateContent, quantity = 1m, unitCode = "ea", unitPrice = 2m } },
    };

    private static async Task<JsonElement> SendAsync(HttpClient client, HttpMethod method, string path,
        object payload, string key, HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(payload) };
        request.Headers.Add("Idempotency-Key", key);
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public bool FailMutations { get; init; }
        public ConcurrentQueue<string> Messages { get; } = new();
        public ConcurrentQueue<Dictionary<string, object?>> Events { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Sink(this);
        public void Dispose() { }

        private sealed class Sink(CapturedLogs owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? error,
                Func<TState, Exception?, string> formatter)
            {
                if (eventId.Name == "MutationSucceeded")
                {
                    if (owner.FailMutations) throw new InvalidOperationException("Synthetic diagnostic failure.");
                    owner.Events.Enqueue(((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary());
                }
                owner.Messages.Enqueue(formatter(state, error) + error?.ToString());
            }
        }
    }

    private sealed record Measurement(string Name, long Value, Dictionary<string, object?> Tags);

    private sealed class CapturedMetrics : IDisposable
    {
        private readonly MeterListener _listener = new();
        public ConcurrentQueue<Measurement> Measurements { get; } = new();
        public IEnumerable<Measurement> Outcomes => Measurements.Where(entry => entry.Name.EndsWith(".outcomes", StringComparison.Ordinal));

        public CapturedMetrics(IMeterFactory scope, bool failOutcomes = false, bool failFailures = false)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == CoreApiMutationDiagnostics.MeterName && ReferenceEquals(instrument.Meter.Scope, scope))
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                if (instrument.Name.EndsWith(".outcomes", StringComparison.Ordinal) ? failOutcomes : failFailures)
                    throw new InvalidOperationException("Synthetic metric listener failure.");
                Measurements.Enqueue(new Measurement(instrument.Name, value, tags.ToArray().ToDictionary()));
            });
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }
}
