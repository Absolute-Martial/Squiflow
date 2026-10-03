using DotNet.Testcontainers.Images;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Application.IdentityAccess;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class DatabaseFailureResponseTests
{
    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status413PayloadTooLarge)]
    public async Task FrameworkRequestFailuresRetainTheirStatusWithoutExposingMessages(int status)
    {
        using var baseline = new WhiteLabelApiFactory();
        using var factory = baseline.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAccountBindingDirectory>();
            services.AddSingleton<IAccountBindingDirectory>(new InvalidRequestBindingProbe(status));
        }));
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/account");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", baseline.CreateToken());
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("invalid_request", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.DoesNotContain("private-request-canary", body);
    }

    [Theory]
    [InlineData("outage", HttpStatusCode.ServiceUnavailable, "database_unavailable")]
    [InlineData("timeout", HttpStatusCode.ServiceUnavailable, "database_unavailable")]
    [InlineData("schema", HttpStatusCode.InternalServerError, "internal_error")]
    public async Task RealProviderFailuresReturnSafeProblems(
        string failure, HttpStatusCode expectedStatus, string expectedCode)
    {
        await using var database = new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
            .WithDatabase("failure_response_tests")
            .WithUsername("postgres")
            .WithPassword("local-integration-test-only")
            .Build();
        await database.StartAsync();
        var connection = new NpgsqlConnectionStringBuilder(database.GetConnectionString())
        {
            Timeout = 2,
            CommandTimeout = 1,
        };
        var logs = new CapturedLogs();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PrimaryDatabase"] = connection.ConnectionString,
            ["Database:ConnectionMode"] = "Direct",
            ["Database:MaximumPoolSize"] = "2",
            ["Database:MinimumPoolSize"] = "0",
            ["Database:ConnectionIdleLifetimeSeconds"] = "300",
            ["Database:ConnectionPruningIntervalSeconds"] = "10",
            ["Database:ConnectionLifetimeSeconds"] = "3600",
            ["Database:CommandTimeoutSeconds"] = "1",
        }).Build();
        await using var source = RuntimeDatabaseConfiguration.From(configuration).CreateDataSource();
        var sql = failure == "schema"
            ? "SELECT * FROM private_failure_canary_missing_table"
            : failure == "timeout" ? "SELECT pg_sleep(10)" : "SELECT 1";
        if (failure == "outage")
            await database.StopAsync();

        using var baseline = new WhiteLabelApiFactory();
        using var factory = baseline.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAccountBindingDirectory>();
                services.AddSingleton<IAccountBindingDirectory>(new DatabaseBindingProbe(source, sql));
                services.AddLogging(logging => logging.AddProvider(logs));
            }));
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/account");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", baseline.CreateToken());
        request.Headers.Add("traceparent", "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(expectedCode, problem.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", problem.RootElement.GetProperty("traceId").GetString()!);
        Assert.DoesNotContain("private_failure_canary", body);
        Assert.DoesNotContain(connection.Password!, body);
        Assert.Contains(logs.Messages, message => message.Contains($"API failure {expectedCode}", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Messages, message => message.Contains("private_failure_canary", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Messages, message => message.Contains(connection.Password!, StringComparison.Ordinal));
        using var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    // The real provider is the property under test; only the account lookup boundary is substituted.
    private sealed class DatabaseBindingProbe(NpgsqlDataSource source, string sql) : IAccountBindingDirectory
    {
        public async Task<AccountBinding?> FindAsync(ExternalIdentity identity, CancellationToken cancellationToken)
        {
            await using var command = source.CreateCommand(sql);
            await command.ExecuteScalarAsync(cancellationToken);
            return null;
        }
    }

    private sealed class InvalidRequestBindingProbe(int status) : IAccountBindingDirectory
    {
        public Task<AccountBinding?> FindAsync(ExternalIdentity identity, CancellationToken cancellationToken) =>
            throw new BadHttpRequestException("private-request-canary", status);
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CapturedLogger(Messages);
        public void Dispose() { }

        private sealed class CapturedLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception) + exception?.ToString());
        }
    }
}
