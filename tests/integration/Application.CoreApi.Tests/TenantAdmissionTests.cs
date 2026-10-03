using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Application.CoreApi.Authorization;
using Application.CoreApi.Composition;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class TenantAdmissionTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("cancel")]
    public async Task OneTenantCannotOccupyAnotherTenantsSlotsAndLeasesRecover(string completion)
    {
        var tenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var account = Guid.NewGuid();
        var secondAccount = Guid.NewGuid();
        var gate = new GatedWorkspace(tenant, completion);
        using var baseline = new WhiteLabelApiFactory();
        foreach (var id in new[] { account, secondAccount })
        {
            baseline.Bind(id.ToString("D"), id);
            baseline.AddTenantMembership(id, tenant, "Tenant with bounded work");
            baseline.AddTenantMembership(id, otherTenant, "Other tenant");
        }
        using var factory = baseline.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(CoreApiAdmission.PermitLimitKey, "4");
            builder.UseSetting(CoreApiAdmission.TenantPermitLimitKey, "1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITenantWorkspaceAuthorization>();
                services.AddSingleton<ITenantWorkspaceAuthorization>(gate);
            });
        });
        using var client = factory.CreateClient();
        using var cancellation = new CancellationTokenSource();
        using var first = Request(tenant, baseline.CreateToken(account.ToString("D")));
        var pending = client.SendAsync(first, cancellation.Token);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var excess = Request(tenant, baseline.CreateToken(secondAccount.ToString("D")));
        using var rejected = await client.SendAsync(excess);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal("tenant_capacity_exceeded", problem.RootElement.GetProperty("code").GetString());
        Assert.True(rejected.Headers.CacheControl?.NoStore);
        Assert.Null(rejected.Headers.RetryAfter);
        Assert.Equal(1, gate.TenantCalls);

        using var other = Request(otherTenant, baseline.CreateToken(account.ToString("D")));
        using var otherResponse = await client.SendAsync(other);
        Assert.Equal(HttpStatusCode.OK, otherResponse.StatusCode);
        using var bootstrap = await client.GetAsync("/api/v1/application/bootstrap");
        Assert.True(bootstrap.Headers.CacheControl?.Public);
        Assert.NotNull(bootstrap.Headers.ETag);
        Assert.Equal(1, factory.Services.GetRequiredService<TenantAdmissionPartitions>().RetainedPartitionCount);

        if (completion == "cancel")
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            await gate.Exited.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        else
        {
            gate.Release.TrySetResult();
            using var completed = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(completion == "success" ? HttpStatusCode.OK : HttpStatusCode.InternalServerError, completed.StatusCode);
        }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            using var retry = Request(tenant, baseline.CreateToken(account.ToString("D")));
            using var response = await client.SendAsync(retry, deadline.Token);
            if (response.StatusCode != HttpStatusCode.TooManyRequests)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            await Task.Yield();
        }
        Assert.Equal(2, gate.TenantCalls);
        Assert.Equal(0, factory.Services.GetRequiredService<TenantAdmissionPartitions>().RetainedPartitionCount);
    }

    [Fact]
    public async Task MissingMembershipCannotConsumeTenantCapacityOrProbePermissions()
    {
        using var factory = new WhiteLabelApiFactory();
        var account = Guid.NewGuid();
        factory.Bind(account.ToString("D"), account);
        var tenant = Guid.NewGuid();
        using var client = factory.CreateClient();
        using var request = Request(tenant, factory.CreateToken(account.ToString("D")));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, factory.GetWorkspaceCheckCount(account, tenant));
        Assert.Equal(0, factory.Services.GetRequiredService<TenantAdmissionPartitions>().RetainedPartitionCount);
    }

    [Fact]
    public void PartitionsAreBoundedRemovedAndReleasedExactlyOnce()
    {
        using var partitions = new TenantAdmissionPartitions(2, 2);
        var tenant = Guid.NewGuid();
        var other = Guid.NewGuid();
        using var first = partitions.TryAcquire(tenant);
        using var second = partitions.TryAcquire(tenant);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Null(partitions.TryAcquire(tenant));
        using var otherLease = partitions.TryAcquire(other);
        Assert.NotNull(otherLease);
        Assert.Null(partitions.TryAcquire(Guid.NewGuid()));
        Assert.Equal(2, partitions.RetainedPartitionCount);
        first.Dispose();
        first.Dispose();
        using var replacement = partitions.TryAcquire(tenant);
        Assert.NotNull(replacement);
        second.Dispose();
        replacement.Dispose();
        Assert.Equal(1, partitions.RetainedPartitionCount);
        otherLease.Dispose();
        Assert.Equal(0, partitions.RetainedPartitionCount);
        using var fresh = partitions.TryAcquire(Guid.NewGuid());
        Assert.NotNull(fresh);
    }

    [Fact]
    public void ShutdownRejectsNewWorkAndAllowsLateLeaseDisposal()
    {
        var partitions = new TenantAdmissionPartitions(1, 1);
        var lease = partitions.TryAcquire(Guid.NewGuid());
        Assert.NotNull(lease);
        partitions.Dispose();
        partitions.Dispose();
        Assert.Equal(0, partitions.RetainedPartitionCount);
        Assert.Throws<ObjectDisposedException>(() => partitions.TryAcquire(Guid.NewGuid()));
        lease.Dispose();
        lease.Dispose();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("257")]
    [InlineData("not-an-integer")]
    public void UnsafeTenantLimitFailsConfiguration(string? value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [CoreApiAdmission.PermitLimitKey] = "32",
            [CoreApiAdmission.TenantPermitLimitKey] = value,
        }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddCoreApiAdmission(configuration));
    }

    [Fact]
    public async Task TenantContractsDescribeCapacityRejectionWithoutChangingAccountContracts()
    {
        using var factory = new WhiteLabelApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject()
                     .Where(path => path.Name.StartsWith("/api/v1/tenants/", StringComparison.Ordinal)))
        {
            foreach (var operation in path.Value.EnumerateObject())
                Assert.True(operation.Value.GetProperty("responses").TryGetProperty("429", out _));
        }
        Assert.False(document.RootElement.GetProperty("paths").GetProperty("/api/v1/account").GetProperty("get")
            .GetProperty("responses").TryGetProperty("429", out _));
    }

    private static HttpRequestMessage Request(Guid tenant, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/tenants/{tenant:D}/workspace");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private sealed class GatedWorkspace(Guid tenant, string completion) : ITenantWorkspaceAuthorization
    {
        private int _calls;
        internal int TenantCalls => Volatile.Read(ref _calls);
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<bool> CanViewAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken)
        {
            if (tenantId == tenant && Interlocked.Increment(ref _calls) == 1)
            {
                Entered.TrySetResult();
                try
                {
                    await Release.Task.WaitAsync(cancellationToken);
                    if (completion == "failure") throw new InvalidOperationException("Synthetic gated failure.");
                }
                finally { Exited.TrySetResult(); }
            }
            return true;
        }
    }
}
