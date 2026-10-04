using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Application.AdminApi.Authorization;
using Application.AdminApi.Composition;
using Application.PlatformAdministration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Application.AdminApi.Tests;

[Collection(AdminApiIntegrationFixtureGroup.Name)]
public sealed class RequestBudgetTests(AdminApiTestEnvironment environment)
{
    [Fact]
    public async Task DeadlineExpirationBeforeHeadersReturnsSafeProblemDetails()
    {
        var authorization = new BlockingPlatformAdminAuthorization();
        var logs = new CapturedLogs();
        using var baseline = environment.CreateFactory(environment.DeviceCertificate);
        using var factory = baseline.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AdminApi:ProtectedRequestTimeoutSeconds", "1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPlatformAdminAuthorization>();
                services.AddSingleton<IPlatformAdminAuthorization>(authorization);
                services.AddLogging(logging => logging.AddProvider(logs));
            });
        });
        using var client = CreateClient(factory);
        using var request = AuthenticatedRequest(environment.CreateToken());
        request.Headers.Add("traceparent", "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01");

        var pending = client.SendAsync(request);
        await authorization.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var response = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        var body = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);

        Assert.True(authorization.CancellationObserved);
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("request_timeout", problem.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("private-admin-timeout-canary", body, StringComparison.Ordinal);
        Assert.DoesNotContain("private-admin-nested-payload-canary", body, StringComparison.Ordinal);
        Assert.DoesNotContain("aaaaaaaa", problem.RootElement.GetProperty("traceId").GetString()!);
        Assert.DoesNotContain(
            logs.Messages,
            message => message.Contains("private-admin-timeout-canary", StringComparison.Ordinal) ||
                message.Contains("private-admin-nested-payload-canary", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CancellationAfterHeadersAbortsWithoutDisclosingExceptions(bool callerCancels, bool ioCancellation)
    {
        var logs = new CapturedLogs();
        BlockingPlatformAdminAuthorization? authorization = null;
        using var baseline = environment.CreateFactory(environment.DeviceCertificate);
        using var factory = baseline.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AdminApi:ProtectedRequestTimeoutSeconds", callerCancels ? "30" : "1");
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpContextAccessor();
                services.RemoveAll<IPlatformAdminAuthorization>();
                services.AddSingleton<IPlatformAdminAuthorization>(provider =>
                    authorization = new BlockingPlatformAdminAuthorization(
                        provider.GetRequiredService<IHttpContextAccessor>(), ioCancellation));
                services.AddLogging(logging => logging.AddProvider(logs));
            });
        });
        using var client = CreateClient(factory);
        var diagnostics = new CapturedExceptionDiagnostics();
        using var subscription = factory.Services.GetRequiredService<DiagnosticListener>().Subscribe(diagnostics);
        using var request = AuthenticatedRequest(environment.CreateToken());
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
            .WaitAsync(TimeSpan.FromSeconds(5));
        using var body = await response.Content.ReadAsStreamAsync();
        var prefix = new byte["started-response".Length];
        await body.ReadExactlyAsync(prefix).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("started-response", System.Text.Encoding.UTF8.GetString(prefix));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var remainder = body.ReadAsync(new byte[1]).AsTask();
        if (callerCancels)
        {
            response.Dispose();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => remainder.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        else
        {
            await Assert.ThrowsAnyAsync<IOException>(() => remainder.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        Assert.NotNull(authorization);
        await authorization.PipelineCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(authorization.CancellationObserved);
        Assert.Empty(diagnostics.Exceptions);
        Assert.DoesNotContain(logs.Messages, message =>
            message.Contains("private-admin-timeout-canary", StringComparison.Ordinal) ||
            message.Contains("private-admin-nested-payload-canary", StringComparison.Ordinal) ||
            message.Contains("private-admin-io-canary", StringComparison.Ordinal) ||
            message.Contains("private-admin-nested-io-payload-canary", StringComparison.Ordinal));
        Assert.Equal(!callerCancels, logs.Messages.Any(message =>
            message.Contains("deadline expired after response headers started", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task DeadlineCancelsRealPostgresAuthorityLookupAndReleasesItsLockWait()
    {
        await environment.ClearAccessAuditAsync();
        var pipelineCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var baseline = environment.CreateFactory(environment.DeviceCertificate);
        using var factory = baseline.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AdminApi:ProtectedRequestTimeoutSeconds", "3");
            builder.UseSetting("AdminApi:MaximumConcurrentRequests", "1");
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IStartupFilter>(new PipelineCompletionFilter(pipelineCompleted)));
        });
        using var client = CreateClient(factory);
        await using var blocker = new NpgsqlConnection(environment.OwnerConnectionString);
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using (var hold = new NpgsqlCommand(
            "LOCK TABLE platform_administration.principals IN ACCESS EXCLUSIVE MODE", blocker, transaction))
        {
            _ = await hold.ExecuteNonQueryAsync();
        }

        await using var observer = new NpgsqlConnection(environment.OwnerConnectionString);
        await observer.OpenAsync();
        await using var wait = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1 FROM pg_locks
                WHERE relation = 'platform_administration.principals'::regclass
                    AND mode = 'AccessShareLock' AND NOT granted)
            """, observer);
        using var observationBudget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var request = AuthenticatedRequest(environment.CreateToken());
        var pending = client.SendAsync(request);
        while ((bool)(await wait.ExecuteScalarAsync(observationBudget.Token))! is false)
        {
            Assert.False(pending.IsCompleted, "The request ended before its PostgreSQL lock wait was observed.");
            await Task.Yield();
        }

        using var response = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        await pipelineCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("request_timeout", problem.RootElement.GetProperty("code").GetString());
        Assert.False((bool)(await wait.ExecuteScalarAsync())!);
        await transaction.RollbackAsync();

        using var retry = AuthenticatedRequest(environment.CreateToken());
        using var recovered = await client.SendAsync(retry).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }

    [Fact]
    public async Task DeadlineDuringAuthenticationReturnsSafeProblemDetailsBeforeEndpointAuthority()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var authorization = new BlockingPlatformAdminAuthorization();
        using var baseline = environment.CreateFactory(environment.DeviceCertificate);
        using var factory = baseline.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AdminApi:ProtectedRequestTimeoutSeconds", "1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPlatformAdminAuthorization>();
                services.AddSingleton<IPlatformAdminAuthorization>(authorization);
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    var validate = options.Events.OnTokenValidated;
                    options.Events.OnTokenValidated = async context =>
                    {
                        await validate(context).ConfigureAwait(false);
                        entered.TrySetResult();
                        await Task.Delay(Timeout.InfiniteTimeSpan, context.HttpContext.RequestAborted)
                            .ConfigureAwait(false);
                    };
                });
            });
        });
        using var client = CreateClient(factory);
        using var request = AuthenticatedRequest(environment.CreateToken());
        var pending = client.SendAsync(request);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var response = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("request_timeout", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(0, authorization.Calls);
    }

    [Fact]
    public async Task ProtectedRequestCompletesWithinDeadline()
    {
        await environment.ClearAccessAuditAsync();
        using var baseline = environment.CreateFactory(environment.DeviceCertificate);
        using var factory = baseline.WithWebHostBuilder(builder =>
            builder.UseSetting("AdminApi:ProtectedRequestTimeoutSeconds", "5"));
        using var client = CreateClient(factory);
        using var request = AuthenticatedRequest(environment.CreateToken());
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var audit = await environment.LatestAuditAsync();
        Assert.Equal((short)PlatformAdminAccessAuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal("authorized", audit.Reason);
        Assert.Equal(environment.PrincipalId, audit.PrincipalId);
        Assert.Equal(environment.DeviceId, audit.DeviceId);
    }

    [Fact]
    public async Task CallerCancellationIsNotConvertedToDeadlineResponse()
    {
        var authorization = new BlockingPlatformAdminAuthorization();
        using var baseline = environment.CreateFactory(environment.DeviceCertificate);
        using var factory = baseline.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AdminApi:ProtectedRequestTimeoutSeconds", "30");
            builder.UseSetting("AdminApi:MaximumConcurrentRequests", "1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPlatformAdminAuthorization>();
                services.AddSingleton<IPlatformAdminAuthorization>(authorization);
                services.AddSingleton<IStartupFilter>(new PipelineCompletionFilter(authorization.PipelineCompleted));
            });
        });
        using var client = CreateClient(factory);
        using var cancellation = new CancellationTokenSource();
        using var request = AuthenticatedRequest(environment.CreateToken());

        var pending = client.SendAsync(request, cancellation.Token);
        await authorization.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await authorization.Exited.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await authorization.PipelineCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(authorization.CancellationObserved);
        using var retry = AuthenticatedRequest(environment.CreateToken());
        using var recovered = await client.SendAsync(retry).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(2, authorization.Calls);
    }

    [Fact]
    public async Task AdmissionCapacityIsReleasedAfterDeadlineCancellation()
    {
        var authorization = new BlockingPlatformAdminAuthorization();
        using var baseline = environment.CreateFactory(environment.DeviceCertificate);
        using var factory = baseline.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AdminApi:ProtectedRequestTimeoutSeconds", "1");
            builder.UseSetting("AdminApi:MaximumConcurrentRequests", "1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPlatformAdminAuthorization>();
                services.AddSingleton<IPlatformAdminAuthorization>(authorization);
                services.AddSingleton<IStartupFilter>(new PipelineCompletionFilter(authorization.PipelineCompleted));
            });
        });
        using var client = CreateClient(factory);
        using var first = AuthenticatedRequest(environment.CreateToken());

        var pending = client.SendAsync(first);
        await authorization.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var timedOut = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.GatewayTimeout, timedOut.StatusCode);
        await authorization.Exited.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await authorization.PipelineCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var retry = AuthenticatedRequest(environment.CreateToken());
        using var recovered = await client.SendAsync(retry).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(2, authorization.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnexpectedExceptionsRetainInternalErrorBehavior(bool ioFailure)
    {
        var logs = new CapturedLogs();
        using var baseline = environment.CreateFactory(environment.DeviceCertificate);
        using var factory = baseline.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AdminApi:ProtectedRequestTimeoutSeconds", "5");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPlatformAdminAuthorization>();
                services.AddSingleton<IPlatformAdminAuthorization>(
                    new ThrowingPlatformAdminAuthorization(
                        ioFailure
                            ? new IOException("private-unexpected-canary")
                            : new InvalidOperationException("private-unexpected-canary")));
                services.AddLogging(logging => logging.AddProvider(logs));
            });
        });
        using var client = CreateClient(factory);
        using var request = AuthenticatedRequest(environment.CreateToken());
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("internal_error", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.DoesNotContain("private-unexpected-canary", body, StringComparison.Ordinal);
        Assert.Contains(logs.Messages, message =>
            message.Contains("Admin API failure internal_error", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Messages, message =>
            message.Contains("private-unexpected-canary", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EveryAuthenticatedRouteHasProtectedBudgetMetadataAndRejectsAnonymousCallers()
    {
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        var endpoints = factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
        Assert.NotEmpty(endpoints);
        var protectedCount = 0;
        foreach (var endpoint in endpoints)
        {
            var access = endpoint.Metadata.GetMetadata<AdminEndpointAccessMetadata>();
            Assert.NotNull(access);
            var authenticated = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0;
            Assert.Equal(authenticated, access.IsProtected);
            if (!authenticated)
            {
                Assert.Equal(AdminEndpointAccess.PublicHealth, access.Access);
                continue;
            }

            protectedCount++;
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
            Assert.Null(endpoint.Metadata.GetMetadata<DisableRequestTimeoutAttribute>());
            Assert.Null(endpoint.Metadata.GetMetadata<RequestTimeoutAttribute>());
            Assert.Null(endpoint.Metadata.GetMetadata<RequestTimeoutPolicy>());
            var path = endpoint.RoutePattern.RawText!;
            foreach (var parameter in endpoint.RoutePattern.Parameters)
            {
                path = path.Replace(
                    "{" + parameter.Name + (parameter.ParameterPolicies.Count > 0 ? ":guid}" : "}"),
                    parameter.ParameterPolicies.Count > 0 ? "11111111-1111-1111-1111-111111111111" : "activate",
                    StringComparison.Ordinal);
            }

            foreach (var method in endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), path);
                request.Headers.Add("X-Platform-Admin", "true");
                request.Headers.Add("X-Admin-Device", "registered");
                using var response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            }
        }

        Assert.True(protectedCount > 0);
    }

    [Fact]
    public async Task ProtectedResponsesRetainNoStore()
    {
        using var factory = environment.CreateFactory(environment.DeviceCertificate);
        using var client = CreateClient(factory);
        using var request = AuthenticatedRequest(environment.CreateToken());
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task PublicHealthRoutesAreOutsideProtectedDeadline()
    {
        var authorization = new SlowReadinessAuthorization(TimeSpan.FromMilliseconds(1200));
        using var baseline = environment.CreateFactory(environment.DeviceCertificate);
        using var factory = baseline.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AdminApi:ProtectedRequestTimeoutSeconds", "1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPlatformAdminAuthorization>();
                services.AddSingleton<IPlatformAdminAuthorization>(authorization);
            });
        });
        using var client = CreateClient(factory);

        using var live = await client.GetAsync("/health/live");
        using var ready = await client.GetAsync("/health/ready").WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.True(authorization.ReadinessCompleted);
        Assert.Equal("no-store", live.Headers.CacheControl?.ToString());
        Assert.Equal("no-store", ready.Headers.CacheControl?.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("121")]
    [InlineData("invalid")]
    public void InvalidProtectedRequestTimeoutFailsStartupConfiguration(string? seconds)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PlatformAdministration"] = "Host=localhost;Database=admin",
                ["AllowedHosts"] = "localhost",
                ["AdminApi:MaximumConcurrentRequests"] = "4",
                ["AdminApi:ProtectedRequestTimeoutSeconds"] = seconds,
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => AdminApiConfiguration.From(configuration));
        Assert.Contains("AdminApi:ProtectedRequestTimeoutSeconds", exception.Message, StringComparison.Ordinal);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

    private static HttpRequestMessage AuthenticatedRequest(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/platform/access");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private sealed class BlockingPlatformAdminAuthorization(
        IHttpContextAccessor? contexts = null,
        bool ioCancellation = false) : IPlatformAdminAuthorization
    {
        private int _calls;

        internal int Calls => Volatile.Read(ref _calls);
        internal bool CancellationObserved { get; private set; }
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource PipelineCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<bool> CanAccessAsync(Guid platformPrincipalId, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) != 1)
            {
                return true;
            }

            if (contexts is not null)
            {
                var context = contexts.HttpContext ?? throw new InvalidOperationException("No active test request.");
                context.Response.OnCompleted(() =>
                {
                    PipelineCompleted.TrySetResult();
                    return Task.CompletedTask;
                });
                await context.Response.WriteAsync("started-response", cancellationToken).ConfigureAwait(false);
                await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            Entered.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
            {
                CancellationObserved = cancellationToken.IsCancellationRequested;
                if (ioCancellation)
                {
                    throw new IOException(
                        "private-admin-io-canary",
                        new InvalidOperationException("private-admin-nested-io-payload-canary", exception));
                }

                throw new OperationCanceledException(
                    "private-admin-timeout-canary",
                    new InvalidOperationException("private-admin-nested-payload-canary", exception),
                    cancellationToken);
            }
            finally
            {
                Exited.TrySetResult();
            }

            throw new InvalidOperationException("The blocked authorization unexpectedly completed.");
        }

        public Task<bool> CanProvisionTenantAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> CanOnboardAccountAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> CanLinkIdentityAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> CanManageMembershipsAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> CanManageTenantLifecycleAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> IsReadyAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class PipelineCompletionFilter(TaskCompletionSource completed) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                try
                {
                    await continuation(context);
                }
                finally
                {
                    completed.TrySetResult();
                }
            });
            next(app);
        };
    }

    private sealed class CapturedExceptionDiagnostics : IObserver<KeyValuePair<string, object?>>
    {
        internal ConcurrentQueue<string> Exceptions { get; } = new();

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Value?.GetType().GetProperty("exception")?.GetValue(value.Value) is Exception exception)
            {
                Exceptions.Enqueue(exception.ToString());
            }
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }

    private sealed class ThrowingPlatformAdminAuthorization(Exception exception) : IPlatformAdminAuthorization
    {
        public Task<bool> CanAccessAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Task.FromException<bool>(exception);

        public Task<bool> CanProvisionTenantAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> CanOnboardAccountAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> CanLinkIdentityAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> CanManageMembershipsAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> CanManageTenantLifecycleAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> IsReadyAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        internal ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturedLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class CapturedLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception) + exception);
        }
    }

    private sealed class SlowReadinessAuthorization(TimeSpan delay) : IPlatformAdminAuthorization
    {
        internal bool ReadinessCompleted { get; private set; }

        public Task<bool> CanAccessAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> CanProvisionTenantAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> CanOnboardAccountAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> CanLinkIdentityAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> CanManageMembershipsAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> CanManageTenantLifecycleAsync(Guid platformPrincipalId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            ReadinessCompleted = true;
            return true;
        }
    }
}
