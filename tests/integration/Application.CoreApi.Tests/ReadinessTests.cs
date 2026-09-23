using System.Net;
using System.Text;
using System.Text.Json;
using Application.CoreApi.Authorization;
using Application.CoreApi.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using OpenFga.Sdk.Client;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class ReadinessTests
{
    private const string StoreId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
    private const string ModelId = "01ARZ3NDEKTSV4RRFFQ69G5FAW";

    [Fact]
    public async Task ReadinessFailsWithoutDependenciesWhileLivenessRemainsHealthy()
    {
        using var factory = new WhiteLabelApiFactory();
        using var client = factory.CreateClient();

        using var readiness = await client.GetAsync("/health/ready");
        var body = await readiness.Content.ReadAsStringAsync();
        using var liveness = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
        Assert.Empty(body);
        Assert.Equal("no-store", readiness.Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);
    }

    [Fact]
    public async Task DatabaseReadinessExecutesAgainstRealPostgres()
    {
        await using var database = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("readiness_tests")
            .WithUsername("postgres")
            .WithPassword("local-integration-test-only")
            .Build();
        await database.StartAsync();
        await using var dataSource = new NpgsqlDataSourceBuilder(database.GetConnectionString()).Build();
        var check = new PrimaryDatabaseReadinessCheck(dataSource);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task ConcurrentReadinessCallsShareOneBoundedDependencyProbe()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<CountingReadinessCheck>();
        services.AddHealthChecks()
            .AddCheck<CountingReadinessCheck>("counted", tags: ["readiness"]);
        using var provider = services.BuildServiceProvider();
        var time = new MutableTimeProvider();
        using var cache = new ReadinessStatusCache(provider.GetRequiredService<HealthCheckService>(), time);

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => cache.IsReadyAsync(CancellationToken.None)));

        Assert.All(results, Assert.True);
        Assert.Equal(1, provider.GetRequiredService<CountingReadinessCheck>().Count);

        time.Advance(TimeSpan.FromSeconds(6));
        Assert.True(await cache.IsReadyAsync(CancellationToken.None));
        Assert.Equal(2, provider.GetRequiredService<CountingReadinessCheck>().Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    public async Task OpenFgaReadinessUsesThePinnedModelAndReportsAvailability(
        HttpStatusCode providerStatus,
        bool expectedHealthy)
    {
        var configuration = OpenFgaAuthorizationConfiguration.From(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization:OpenFga:ApiUrl"] = "http://localhost:8080",
                ["Authorization:OpenFga:StoreId"] = StoreId,
                ["Authorization:OpenFga:AuthorizationModelId"] = ModelId,
                ["Authorization:OpenFga:RequestTimeoutSeconds"] = "1",
                ["Authorization:OpenFga:MaximumRetries"] = "0",
                ["Authorization:OpenFga:MinimumRetryDelayMilliseconds"] = "100",
                ["Authorization:OpenFga:CredentialMethod"] = "None",
            })
            .Build());
        var handler = new ModelHandler(providerStatus);
        using var httpClient = new HttpClient(handler);
        using var client = new OpenFgaClient(configuration.ToClientConfiguration(), httpClient);
        var check = new OpenFgaReadinessCheck(client, configuration);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(expectedHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy, result.Status);
        Assert.Equal($"/stores/{StoreId}/authorization-models/{ModelId}", handler.RequestPath);
    }

    private sealed class ModelHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        internal string? RequestPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestPath = request.RequestUri?.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        authorization_model = new
                        {
                            id = ModelId,
                            schema_version = "1.1",
                            type_definitions = Array.Empty<object>(),
                        },
                    }),
                    Encoding.UTF8,
                    "application/json"),
            });
        }
    }

    private sealed class CountingReadinessCheck : IHealthCheck
    {
        private int _count;

        internal int Count => Volatile.Read(ref _count);

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
            return HealthCheckResult.Healthy();
        }
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private long _utcTicks = new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero).Ticks;

        public override DateTimeOffset GetUtcNow() =>
            new(Interlocked.Read(ref _utcTicks), TimeSpan.Zero);

        internal void Advance(TimeSpan duration) => Interlocked.Add(ref _utcTicks, duration.Ticks);
    }
}
