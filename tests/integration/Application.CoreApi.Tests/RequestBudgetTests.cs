using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Application.CoreApi.Composition;
using Application.IdentityAccess;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class RequestBudgetTests
{
    [Fact]
    public async Task SlowProtectedBodyReadTimesOutBeforeCustomerMutation()
    {
        using var baseline = new WhiteLabelApiFactory();
        var account = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        baseline.Bind("slow-body-subject", account);
        baseline.AddTenantMembership(account, tenant, "Fixture tenant");
        baseline.SetCustomerDecision(account, tenant, "createOrganization", true);
        using var factory = baseline.WithWebHostBuilder(builder =>
            builder.UseSetting(CoreApiRequestBudgets.TimeoutKey, "1"));
        using var client = factory.CreateClient();
        using var slowBody = new SlowBodyStream();
        var completed = await factory.Server.SendAsync(context =>
        {
            context.Request.Method = "POST";
            context.Request.Path = $"/api/v1/tenants/{tenant:D}/customers/organizations";
            context.Request.ContentType = "application/json";
            context.Request.Headers.Authorization = "Bearer " + baseline.CreateToken("slow-body-subject");
            context.Request.Headers["Idempotency-Key"] = "slow-body-key";
            context.Request.Body = slowBody;
        }).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(slowBody.CancellationObserved);
        Assert.Equal(StatusCodes.Status504GatewayTimeout, completed.Response.StatusCode);
        Assert.Equal("no-store", completed.Response.Headers.CacheControl.ToString());
        using var problem = await JsonDocument.ParseAsync(completed.Response.Body);
        Assert.Equal("request_timeout", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(0, baseline.GetCustomerStoreCallCount(tenant));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeadlineAndClientCancellationReleaseCapacityWithoutDisclosingExceptions(bool clientCancels)
    {
        var directory = new SlowBindingDirectory();
        var logs = new CapturedLogs();
        using var baseline = new WhiteLabelApiFactory();
        using var factory = baseline.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(CoreApiRequestBudgets.TimeoutKey, "1");
            builder.UseSetting(CoreApiAdmission.PermitLimitKey, "1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAccountBindingDirectory>();
                services.AddSingleton<IAccountBindingDirectory>(directory);
                services.AddLogging(logging => logging.AddProvider(logs));
            });
        });
        using var client = factory.CreateClient();
        using var cancellation = new CancellationTokenSource();
        using var request = AuthenticatedRequest(baseline.CreateToken());
        request.Headers.Add("traceparent", "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01");
        var pending = client.SendAsync(request, cancellation.Token);
        await directory.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var bootstrap = await client.GetAsync("/api/v1/application/bootstrap");
        using var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, bootstrap.StatusCode);
        Assert.True(bootstrap.Headers.CacheControl?.Public);
        Assert.NotNull(bootstrap.Headers.ETag);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        if (clientCancels)
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        }
        else
        {
            using var response = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var body = await response.Content.ReadAsStringAsync();
            using var problem = JsonDocument.Parse(body);
            Assert.Equal("request_timeout", problem.RootElement.GetProperty("code").GetString());
            Assert.DoesNotContain("aaaaaaaa", problem.RootElement.GetProperty("traceId").GetString()!);
            Assert.DoesNotContain("private-timeout-canary", body);
        }
        await directory.Exited.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var recoveryBudget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            using var retry = AuthenticatedRequest(baseline.CreateToken());
            using var recovered = await client.SendAsync(retry, recoveryBudget.Token);
            if (recovered.StatusCode != HttpStatusCode.ServiceUnavailable)
            {
                Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
                break;
            }
            await Task.Yield();
        }
        Assert.Equal(2, directory.Calls);
        Assert.DoesNotContain(logs.Messages, message => message.Contains("private-timeout-canary", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnlyProtectedOpenApiOperationsDeclareTimeoutResponses()
    {
        using var factory = new WhiteLabelApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject().Where(value => value.Name is "get" or "post" or "put"))
            {
                var secured = operation.Value.TryGetProperty("security", out var security) && security.GetArrayLength() > 0;
                Assert.Equal(secured, operation.Value.GetProperty("responses").TryGetProperty("504", out _));
            }
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("121")]
    [InlineData("invalid")]
    public void UnsafeRequestBudgetConfigurationFailsStartup(string? seconds)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [CoreApiRequestBudgets.TimeoutKey] = seconds,
        }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddCoreApiRequestBudgets(config));
    }

    private static HttpRequestMessage AuthenticatedRequest(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/account");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private sealed class SlowBindingDirectory : IAccountBindingDirectory
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<AccountBinding?> FindAsync(ExternalIdentity identity, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                Entered.TrySetResult();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
                catch (OperationCanceledException error)
                {
                    throw new OperationCanceledException("private-timeout-canary", error, cancellationToken);
                }
                finally { Exited.TrySetResult(); }
            }
            return new AccountBinding(Guid.NewGuid(), AccountAvailability.Active);
        }
    }

    private sealed class SlowBodyStream : MemoryStream
    {
        public override bool CanSeek => false;
        public bool CancellationObserved { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            catch (OperationCanceledException)
            {
                CancellationObserved = cancellationToken.IsCancellationRequested;
                throw;
            }
            throw new InvalidOperationException("The blocked read unexpectedly completed.");
        }
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
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception) + exception);
        }
    }
}
