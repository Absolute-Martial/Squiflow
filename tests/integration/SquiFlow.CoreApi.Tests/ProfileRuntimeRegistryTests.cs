using Autofac;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SquiFlow.CoreApi.Composition;
using SquiFlow.Tenancy;
using Xunit;

namespace SquiFlow.CoreApi.Tests;

public sealed class ProfileRuntimeRegistryTests
{
    [Fact]
    public async Task ConcurrentFirstUseBuildsOneRuntimeAndKeepsTenantContextOperationScoped()
    {
        await using var root = new ContainerBuilder().Build();
        await using var registry = CreateRegistry(root);
        var buildCount = 0;
        var definition = new ProfileRuntimeDefinition(
            new ProfileRuntimeKey("shared-orders", 1),
            _ => Interlocked.Increment(ref buildCount));
        var contexts = await Task.WhenAll(
            CreateTenantContextAsync(),
            CreateTenantContextAsync());

        var acquisitions = Enumerable.Range(0, 24)
            .Select(index => registry.AcquireAsync(
                definition,
                contexts[index % contexts.Length]).AsTask())
            .ToArray();
        var leases = await Task.WhenAll(acquisitions);

        Assert.Equal(1, Volatile.Read(ref buildCount));
        Assert.Equal(1, registry.RetainedRuntimeCount);
        Assert.Equal(
            contexts[0],
            leases[0].Resolve<TenantContext>());
        Assert.Equal(
            contexts[1],
            leases[1].Resolve<TenantContext>());

        foreach (var lease in leases)
        {
            await lease.DisposeAsync();
        }
    }

    [Fact]
    public async Task RetirementRejectsNewLeasesAndWaitsForTheLastActiveLease()
    {
        await using var root = new ContainerBuilder().Build();
        await using var registry = CreateRegistry(root);
        var tracker = new DisposalTracker();
        var definition = new ProfileRuntimeDefinition(
            new ProfileRuntimeKey("billing-vendor-a", 4),
            builder => builder
                .RegisterInstance(tracker)
                .OwnedByLifetimeScope());
        var context = await CreateTenantContextAsync();
        var lease = await registry.AcquireAsync(definition, context);
        _ = lease.Resolve<DisposalTracker>();

        var retirement = registry.RetireAsync(definition.Key);

        Assert.False(retirement.IsCompleted);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await registry.AcquireAsync(definition, context));
        Assert.Equal(0, tracker.DisposeCount);

        await lease.DisposeAsync();
        Assert.True(await retirement);
        Assert.Equal(1, tracker.DisposeCount);
        Assert.Equal(0, registry.RetainedRuntimeCount);
    }

    [Fact]
    public async Task RetainedRuntimeLimitRejectsColdBuildUntilCapacityIsReleased()
    {
        await using var root = new ContainerBuilder().Build();
        await using var registry = CreateRegistry(
            root,
            maximumRetainedRuntimes: 1);
        var context = await CreateTenantContextAsync();
        var firstDefinition = Definition("first", 1);
        var secondDefinition = Definition("second", 1);
        var firstLease = await registry.AcquireAsync(firstDefinition, context);

        await Assert.ThrowsAsync<ProfileRuntimeCapacityException>(async () =>
            await registry.AcquireAsync(secondDefinition, context));

        await firstLease.DisposeAsync();
        Assert.True(await registry.RetireAsync(firstDefinition.Key));

        await using var secondLease =
            await registry.AcquireAsync(secondDefinition, context);
        Assert.Equal(secondDefinition.Key, secondLease.Key);
    }

    [Fact]
    public async Task IdleRuntimeIsRetiredWithoutForcingGarbageCollection()
    {
        await using var root = new ContainerBuilder().Build();
        await using var registry = CreateRegistry(root);
        var definition = Definition("idle", 1);
        var context = await CreateTenantContextAsync();
        await using (var lease = await registry.AcquireAsync(definition, context))
        {
            Assert.Equal(definition.Key, lease.Key);
        }
        var retired = await registry.RetireIdleAsync(
            DateTimeOffset.UtcNow.AddHours(1));

        Assert.Equal(1, retired);
        Assert.Equal(0, registry.RetainedRuntimeCount);
    }

    [Fact]
    public async Task FailedBuildIsRemovedSoTheSameImmutableKeyCanRetry()
    {
        await using var root = new ContainerBuilder().Build();
        await using var registry = CreateRegistry(root);
        var attempts = 0;
        var definition = new ProfileRuntimeDefinition(
            new ProfileRuntimeKey("retryable", 2),
            _ =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    throw new InvalidOperationException("Synthetic build failure.");
                }
            });
        var context = await CreateTenantContextAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await registry.AcquireAsync(definition, context));
        Assert.Equal(0, registry.RetainedRuntimeCount);

        await using var lease = await registry.AcquireAsync(definition, context);
        Assert.Equal(2, Volatile.Read(ref attempts));
        Assert.Equal(definition.Key, lease.Key);
    }

    [Fact]
    public async Task BuildConcurrencyIsBoundedAcrossDifferentRuntimeKeys()
    {
        await using var root = new ContainerBuilder().Build();
        await using var registry = CreateRegistry(
            root,
            maximumRetainedRuntimes: 2,
            maximumConcurrentBuilds: 1);
        using var releaseFirstBuild = new ManualResetEventSlim();
        var firstBuildEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var currentBuilds = 0;
        var maximumObservedBuilds = 0;
        var context = await CreateTenantContextAsync();

        ProfileRuntimeDefinition CreateBlockingDefinition(string fingerprint) =>
            new(
                new ProfileRuntimeKey(fingerprint, 1),
                _ =>
                {
                    var current = Interlocked.Increment(ref currentBuilds);
                    UpdateMaximum(ref maximumObservedBuilds, current);
                    firstBuildEntered.TrySetResult(true);
                    releaseFirstBuild.Wait(TimeSpan.FromSeconds(5));
                    Interlocked.Decrement(ref currentBuilds);
                });

        var first = Task.Run(async () =>
            await registry.AcquireAsync(
                CreateBlockingDefinition("build-one"),
                context));
        await firstBuildEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = Task.Run(async () =>
            await registry.AcquireAsync(
                CreateBlockingDefinition("build-two"),
                context));

        Assert.Equal(1, Volatile.Read(ref maximumObservedBuilds));
        releaseFirstBuild.Set();

        var leases = await Task.WhenAll(first, second);
        Assert.Equal(1, maximumObservedBuilds);
        foreach (var lease in leases)
        {
            await lease.DisposeAsync();
        }
    }

    [Fact]
    public async Task RuntimeLifecycleEmitsCollectableMetricsWithoutTenantIdentifiers()
    {
        var measurements = new ConcurrentBag<string>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == ProfileRuntimeTelemetry.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>(
            (instrument, _, _, _) => measurements.Add(instrument.Name));
        listener.SetMeasurementEventCallback<double>(
            (instrument, _, _, _) => measurements.Add(instrument.Name));
        listener.Start();

        await using var root = new ContainerBuilder().Build();
        await using var registry = CreateRegistry(root);
        var definition = Definition("observable", 1);
        var context = await CreateTenantContextAsync();
        await using (var lease = await registry.AcquireAsync(definition, context))
        {
            Assert.Equal(definition.Key, lease.Key);
        }
        await using (var lease = await registry.AcquireAsync(definition, context))
        {
            Assert.Equal(definition.Key, lease.Key);
        }

        Assert.True(await registry.RetireAsync(definition.Key));

        Assert.Contains("squiflow.profile_runtime.builds", measurements);
        Assert.Contains("squiflow.profile_runtime.build_duration", measurements);
        Assert.Contains("squiflow.profile_runtime.acquisitions", measurements);
        Assert.Contains("squiflow.profile_runtime.cache_hits", measurements);
        Assert.Contains("squiflow.profile_runtime.cache_misses", measurements);
        Assert.Contains("squiflow.profile_runtime.active_leases", measurements);
        Assert.Contains("squiflow.profile_runtime.retirements", measurements);
    }

    [Fact]
    public async Task ShutdownDrainTimeoutDoesNotWaitForeverForAStuckBuildCallback()
    {
        await using var root = new ContainerBuilder().Build();
        var registry = CreateRegistry(
            root,
            shutdownDrainTimeout: TimeSpan.FromMilliseconds(50));
        using var releaseBuild = new ManualResetEventSlim();
        var buildEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var definition = new ProfileRuntimeDefinition(
            new ProfileRuntimeKey("slow-build", 1),
            _ =>
            {
                buildEntered.TrySetResult(true);
                releaseBuild.Wait(TimeSpan.FromSeconds(5));
            });
        var context = await CreateTenantContextAsync();
        var acquisition = Task.Run(async () =>
            await registry.AcquireAsync(definition, context));
        await buildEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var elapsed = Stopwatch.StartNew();
        await registry.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        elapsed.Stop();
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(1));
        releaseBuild.Set();

        try
        {
            await using var lease =
                await acquisition.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (InvalidOperationException)
        {
            // Retirement may win the race after the trusted registration callback returns.
        }
    }

    private static ProfileRuntimeRegistry CreateRegistry(
        ILifetimeScope root,
        int maximumRetainedRuntimes = 8,
        int maximumConcurrentBuilds = 2,
        TimeSpan? shutdownDrainTimeout = null)
    {
        var options = Options.Create(new ProfileRuntimeOptions
        {
            MaximumRetainedRuntimes = maximumRetainedRuntimes,
            MaximumConcurrentBuilds = maximumConcurrentBuilds,
            IdleRetention = TimeSpan.FromMinutes(5),
            MaintenanceInterval = TimeSpan.FromMinutes(1),
            ShutdownDrainTimeout = shutdownDrainTimeout ?? TimeSpan.FromSeconds(1),
        });

        return new ProfileRuntimeRegistry(
            root,
            options,
            NullLogger<ProfileRuntimeRegistry>.Instance,
            TimeProvider.System);
    }

    private static ProfileRuntimeDefinition Definition(
        string fingerprint,
        long revision) =>
        new(new ProfileRuntimeKey(fingerprint, revision), _ => { });

    private static async Task<TenantContext> CreateTenantContextAsync()
    {
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var resolver = new ResolveTenantContext(
            new ActiveMembershipDirectory(accountId, tenantId));

        return await resolver.ExecuteAsync(
                   accountId,
                   tenantId,
                   CancellationToken.None)
               ?? throw new InvalidOperationException("The synthetic membership was not resolved.");
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        var observed = Volatile.Read(ref maximum);
        while (candidate > observed)
        {
            var previous = Interlocked.CompareExchange(
                ref maximum,
                candidate,
                observed);
            if (previous == observed)
            {
                return;
            }

            observed = previous;
        }
    }

    private sealed class DisposalTracker : IDisposable
    {
        private int _disposeCount;

        internal int DisposeCount => Volatile.Read(ref _disposeCount);

        public void Dispose()
        {
            Interlocked.Increment(ref _disposeCount);
        }
    }

    private sealed class ActiveMembershipDirectory(Guid accountId, Guid tenantId)
        : ITenantMembershipDirectory
    {
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(
            Guid requestedAccountId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<TenantMembership> memberships = requestedAccountId == accountId
                ? [new TenantMembership(tenantId, "Synthetic Tenant")]
                : [];
            return Task.FromResult(memberships);
        }

        public Task<bool> IsActiveAsync(
            Guid requestedAccountId,
            Guid requestedTenantId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                requestedAccountId == accountId && requestedTenantId == tenantId);
        }
    }
}
