using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Application.CoreApi.Composition;
using Application.IdentityAccess;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class AdmissionTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("cancel")]
    public async Task SaturationRejectsWithoutEnteringHandlerAndReleasesPermits(string completion)
    {
        var directory = new GatedBindingDirectory(completion);
        using var baseline = new WhiteLabelApiFactory();
        using var factory = baseline.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(CoreApiAdmission.PermitLimitKey, "1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAccountBindingDirectory>();
                services.AddSingleton<IAccountBindingDirectory>(directory);
            });
        });
        using var client = factory.CreateClient();
        using var cancellation = new CancellationTokenSource();
        using var first = AuthenticatedRequest(baseline.CreateToken());
        var inFlight = client.SendAsync(first, cancellation.Token);
        await directory.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Even unauthenticated work is bounded before expensive authentication/authorization begins.
        using var rejected = await client.GetAsync("/api/v1/account").WaitAsync(TimeSpan.FromSeconds(5));
        using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, rejected.StatusCode);
        Assert.Equal("api_capacity_exceeded", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal("no-store", rejected.Headers.CacheControl?.ToString());
        Assert.Equal(1, directory.Calls);

        using var live = await client.GetAsync("/health/live");
        using var bootstrap = await client.GetAsync("/api/v1/application/bootstrap");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, bootstrap.StatusCode);
        Assert.True(bootstrap.Headers.CacheControl?.Public);
        Assert.NotNull(bootstrap.Headers.ETag);

        if (completion == "cancel")
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inFlight);
            await directory.Exited.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        else
        {
            directory.Release.TrySetResult();
            using var completed = await inFlight.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(completion == "success" ? HttpStatusCode.OK : HttpStatusCode.InternalServerError,
                completed.StatusCode);
        }

        // Client cancellation can complete before the server finally block returns its lease.
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            using var retry = AuthenticatedRequest(baseline.CreateToken());
            using var response = await client.SendAsync(retry, deadline.Token);
            if (response.StatusCode != HttpStatusCode.ServiceUnavailable)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            await Task.Yield();
        }
        Assert.Equal(2, directory.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("257")]
    [InlineData("not-an-integer")]
    public void UnsafeAdmissionConfigurationFailsStartup(string? permits)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { [CoreApiAdmission.PermitLimitKey] = permits }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddCoreApiAdmission(configuration));
    }

    private static HttpRequestMessage AuthenticatedRequest(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/account");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private sealed class GatedBindingDirectory(string completion) : IAccountBindingDirectory
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<AccountBinding?> FindAsync(ExternalIdentity identity, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                Entered.TrySetResult();
                try
                {
                    await Release.Task.WaitAsync(cancellationToken);
                    if (completion == "failure")
                        throw new InvalidOperationException("Synthetic lookup failure.");
                }
                finally
                {
                    Exited.TrySetResult();
                }
            }
            return new AccountBinding(Guid.NewGuid(), AccountAvailability.Active);
        }
    }
}
