using System.Collections.Concurrent;
using System.Diagnostics;
using Autofac;
using Microsoft.Extensions.Options;
using SquiFlow.Tenancy;

namespace SquiFlow.CoreApi.Composition;

internal sealed class ProfileRuntimeRegistry : IAsyncDisposable
{
    private static readonly Action<ILogger, string, long, Exception?> RuntimeBuilt =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(7100, nameof(RuntimeBuilt)),
            "Built profile runtime {ImplementationFingerprint} revision {ImplementationRevision}.");

    private static readonly Action<ILogger, string, long, Exception?> RuntimeBuildFailed =
        LoggerMessage.Define<string, long>(
            LogLevel.Error,
            new EventId(7101, nameof(RuntimeBuildFailed)),
            "Failed to build profile runtime {ImplementationFingerprint} revision {ImplementationRevision}.");

    private static readonly Action<ILogger, string, long, Exception?> RuntimeRetired =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(7102, nameof(RuntimeRetired)),
            "Retired profile runtime {ImplementationFingerprint} revision {ImplementationRevision}.");

    private static readonly Action<ILogger, string, long, Exception?> RuntimeDisposalFailed =
        LoggerMessage.Define<string, long>(
            LogLevel.Error,
            new EventId(7103, nameof(RuntimeDisposalFailed)),
            "Failed to dispose profile runtime {ImplementationFingerprint} revision {ImplementationRevision}.");

    private static readonly Action<ILogger, TimeSpan, Exception?> RuntimeDrainTimedOut =
        LoggerMessage.Define<TimeSpan>(
            LogLevel.Warning,
            new EventId(7104, nameof(RuntimeDrainTimedOut)),
            "Profile runtime shutdown drain exceeded {ShutdownDrainTimeout}; remaining runtimes will be forced closed.");

    private static readonly Action<ILogger, string, long, Exception?> RuntimeForcedCloseFailed =
        LoggerMessage.Define<string, long>(
            LogLevel.Error,
            new EventId(7105, nameof(RuntimeForcedCloseFailed)),
            "Forced close failed for profile runtime {ImplementationFingerprint} revision {ImplementationRevision}; shutdown will continue closing other runtimes.");

    private readonly ConcurrentDictionary<ProfileRuntimeKey, RuntimeSlot> _runtimes = [];
    private readonly ILifetimeScope _applicationScope;
    private readonly ProfileRuntimeOptions _options;
    private readonly ILogger<ProfileRuntimeRegistry> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _runtimeCapacity;
    private readonly SemaphoreSlim _buildCapacity;
    private readonly CancellationTokenSource _stopping = new();
    private int _accepting = 1;
    private int _disposed;

    public ProfileRuntimeRegistry(
        ILifetimeScope applicationScope,
        IOptions<ProfileRuntimeOptions> options,
        ILogger<ProfileRuntimeRegistry> logger,
        TimeProvider timeProvider)
    {
        _applicationScope = applicationScope;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;
        _runtimeCapacity = new SemaphoreSlim(
            _options.MaximumRetainedRuntimes,
            _options.MaximumRetainedRuntimes);
        _buildCapacity = new SemaphoreSlim(
            _options.MaximumConcurrentBuilds,
            _options.MaximumConcurrentBuilds);
    }

    internal int RetainedRuntimeCount => _runtimes.Count;

    internal async ValueTask<ProfileRuntimeLease> AcquireAsync(
        ProfileRuntimeDefinition definition,
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(tenantContext);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfNotAccepting();

        var slot = GetOrCreateSlot(definition, cancellationToken);
        if (slot.IsRetiring)
        {
            throw new InvalidOperationException(
                $"Profile runtime {definition.Key} is retiring and cannot accept new operations.");
        }

        RuntimeEntry runtime;
        try
        {
            runtime = await slot
                .GetRuntimeAsync()
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            if (slot.BuildTask.IsCompleted && !slot.BuildTask.IsCompletedSuccessfully)
            {
                RemoveSlot(slot);
            }

            throw;
        }

        if (!runtime.TryAcquire(_timeProvider.GetUtcNow()))
        {
            throw new InvalidOperationException(
                $"Profile runtime {definition.Key} is retiring and cannot accept new operations.");
        }

        ILifetimeScope? operationScope = null;
        try
        {
            operationScope = runtime.Scope.BeginLifetimeScope(builder =>
            {
                builder.RegisterInstance(tenantContext).AsSelf().ExternallyOwned();
                builder.RegisterInstance(definition.Key).AsSelf().ExternallyOwned();
            });

            ProfileRuntimeTelemetry.Acquisitions.Add(1);
            ProfileRuntimeTelemetry.ActiveLeases.Add(1);

            return new ProfileRuntimeLease(
                definition.Key,
                operationScope,
                async () =>
                {
                    ProfileRuntimeTelemetry.ActiveLeases.Add(-1);
                    await runtime.ReleaseAsync(_timeProvider.GetUtcNow()).ConfigureAwait(false);
                });
        }
        catch
        {
            if (operationScope is not null)
            {
                await operationScope.DisposeAsync().ConfigureAwait(false);
            }

            await runtime.ReleaseAsync(_timeProvider.GetUtcNow()).ConfigureAwait(false);
            throw;
        }
    }

    internal async Task<bool> RetireAsync(
        ProfileRuntimeKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_runtimes.TryGetValue(key, out var slot))
        {
            return false;
        }

        var (started, retirement) = slot.BeginRetirement(RetireSlotAsync);
        await retirement.WaitAsync(cancellationToken).ConfigureAwait(false);
        return started;
    }

    internal async Task<int> RetireIdleAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var retired = 0;
        foreach (var slot in _runtimes.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!slot.TryGetReadyRuntime(out var runtime) ||
                !runtime.IsIdle(now, _options.IdleRetention))
            {
                continue;
            }

            var (started, retirement) = slot.BeginRetirement(RetireSlotAsync);
            if (!started)
            {
                continue;
            }

            ProfileRuntimeTelemetry.IdleAgeAtRetirement.Record(
                runtime.GetIdleAge(now).TotalSeconds);
            await retirement.WaitAsync(cancellationToken).ConfigureAwait(false);
            retired++;
        }

        return retired;
    }

    internal async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _accepting, 0) != 0)
        {
            _stopping.Cancel();
        }

        var retirements = _runtimes.Values
            .Select(slot => slot.BeginRetirement(RetireSlotAsync).Retirement)
            .ToArray();

        if (retirements.Length > 0)
        {
            await Task.WhenAll(retirements)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        var drained = true;
        using var drain = new CancellationTokenSource(_options.ShutdownDrainTimeout);
        try
        {
            await StopAsync(drain.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (drain.IsCancellationRequested)
        {
            drained = false;
            RuntimeDrainTimedOut(_logger, _options.ShutdownDrainTimeout, null);
            await ForceCloseRemainingAsync().ConfigureAwait(false);
        }
        finally
        {
            // A registration callback already running cannot be aborted safely. If the
            // drain timed out, keep these small coordination objects alive until the
            // process exits so a late build can finish its cleanup without racing a
            // disposed semaphore or cancellation source.
            if (drained)
            {
                _stopping.Dispose();
                _runtimeCapacity.Dispose();
                _buildCapacity.Dispose();
            }
        }
    }

    private RuntimeSlot GetOrCreateSlot(
        ProfileRuntimeDefinition definition,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            ThrowIfNotAccepting();
            if (_runtimes.TryGetValue(definition.Key, out var existing))
            {
                ProfileRuntimeTelemetry.CacheHits.Add(1);
                return existing;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!_runtimeCapacity.Wait(0, cancellationToken))
            {
                ProfileRuntimeTelemetry.CapacityRejections.Add(1);
                throw new ProfileRuntimeCapacityException(_options.MaximumRetainedRuntimes);
            }

            var candidate = new RuntimeSlot(
                definition.Key,
                () => BuildRuntimeAsync(definition));

            if (_runtimes.TryAdd(definition.Key, candidate))
            {
                ProfileRuntimeTelemetry.RetainedRuntimes.Add(1);
                ProfileRuntimeTelemetry.CacheMisses.Add(1);
                return candidate;
            }

            _runtimeCapacity.Release();
        }
    }

    private async Task<RuntimeEntry> BuildRuntimeAsync(ProfileRuntimeDefinition definition)
    {
        var startedAt = Stopwatch.GetTimestamp();
        ProfileRuntimeTelemetry.BuildsInProgress.Add(1);

        try
        {
            await _buildCapacity.WaitAsync(_stopping.Token).ConfigureAwait(false);
            try
            {
                var scope = _applicationScope.BeginLifetimeScope(
                    definition.Key,
                    definition.Configure);
                var runtime = new RuntimeEntry(
                    definition.Key,
                    scope,
                    _timeProvider.GetUtcNow(),
                    _logger);

                ProfileRuntimeTelemetry.Builds.Add(1);
                RuntimeBuilt(
                    _logger,
                    definition.Key.ImplementationFingerprint,
                    definition.Key.ImplementationRevision,
                    null);
                return runtime;
            }
            finally
            {
                _buildCapacity.Release();
            }
        }
        catch (Exception exception)
        {
            ProfileRuntimeTelemetry.BuildFailures.Add(1);
            RuntimeBuildFailed(
                _logger,
                definition.Key.ImplementationFingerprint,
                definition.Key.ImplementationRevision,
                exception);
            throw;
        }
        finally
        {
            ProfileRuntimeTelemetry.BuildsInProgress.Add(-1);
            ProfileRuntimeTelemetry.BuildDuration.Record(
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }

    private async Task RetireSlotAsync(RuntimeSlot slot)
    {
        try
        {
            var runtime = await slot.GetRuntimeAsync().ConfigureAwait(false);
            await runtime.RetireAsync().ConfigureAwait(false);
            ProfileRuntimeTelemetry.Retirements.Add(1);
            RuntimeRetired(
                _logger,
                slot.Key.ImplementationFingerprint,
                slot.Key.ImplementationRevision,
                null);
        }
        finally
        {
            RemoveSlot(slot);
        }
    }

    private void RemoveSlot(RuntimeSlot slot)
    {
        if (!_runtimes.TryRemove(
                new KeyValuePair<ProfileRuntimeKey, RuntimeSlot>(slot.Key, slot)))
        {
            return;
        }

        ProfileRuntimeTelemetry.RetainedRuntimes.Add(-1);
        _runtimeCapacity.Release();
    }

    private async Task ForceCloseRemainingAsync()
    {
        foreach (var slot in _runtimes.Values)
        {
            try
            {
                if (slot.TryGetReadyRuntime(out var runtime))
                {
                    await runtime.ForceDisposeAsync().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
                // A build that had not started was canceled by shutdown.
            }
            catch (Exception exception)
            {
                RuntimeForcedCloseFailed(
                    _logger,
                    slot.Key.ImplementationFingerprint,
                    slot.Key.ImplementationRevision,
                    exception);
            }
            finally
            {
                RemoveSlot(slot);
            }
        }
    }

    private void ThrowIfNotAccepting()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);

        if (Volatile.Read(ref _accepting) == 0)
        {
            throw new InvalidOperationException(
                "The profile runtime registry is stopping and cannot accept new operations.");
        }
    }

    private sealed class RuntimeSlot(
        ProfileRuntimeKey key,
        Func<Task<RuntimeEntry>> build)
    {
        private readonly object _gate = new();
        private readonly Lazy<Task<RuntimeEntry>> _runtime = new(
            () => Task.Run(build),
            LazyThreadSafetyMode.ExecutionAndPublication);
        private Task? _retirement;

        internal ProfileRuntimeKey Key { get; } = key;

        internal Task<RuntimeEntry> BuildTask => _runtime.Value;

        internal bool IsRetiring
        {
            get
            {
                lock (_gate)
                {
                    return _retirement is not null;
                }
            }
        }

        internal Task<RuntimeEntry> GetRuntimeAsync() => _runtime.Value;

        internal bool TryGetReadyRuntime(out RuntimeEntry runtime)
        {
            if (!_runtime.IsValueCreated || !_runtime.Value.IsCompletedSuccessfully)
            {
                runtime = null!;
                return false;
            }

            runtime = _runtime.Value.Result;
            return true;
        }

        internal (bool Started, Task Retirement) BeginRetirement(
            Func<RuntimeSlot, Task> retire)
        {
            lock (_gate)
            {
                if (_retirement is not null)
                {
                    return (false, _retirement);
                }

                _retirement = retire(this);
                return (true, _retirement);
            }
        }
    }

    private sealed class RuntimeEntry(
        ProfileRuntimeKey key,
        ILifetimeScope scope,
        DateTimeOffset createdAt,
        ILogger logger)
    {
        private readonly object _gate = new();
        private readonly TaskCompletionSource<bool> _disposed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _activeLeases;
        private DateTimeOffset _lastUsedAt = createdAt;
        private bool _retiring;
        private bool _disposalStarted;

        internal ILifetimeScope Scope { get; } = scope;

        internal bool TryAcquire(DateTimeOffset now)
        {
            lock (_gate)
            {
                if (_retiring)
                {
                    return false;
                }

                _activeLeases++;
                _lastUsedAt = now;
                return true;
            }
        }

        internal bool IsIdle(DateTimeOffset now, TimeSpan idleRetention)
        {
            lock (_gate)
            {
                return !_retiring &&
                       _activeLeases == 0 &&
                       now - _lastUsedAt >= idleRetention;
            }
        }

        internal TimeSpan GetIdleAge(DateTimeOffset now)
        {
            lock (_gate)
            {
                return now - _lastUsedAt;
            }
        }

        internal async ValueTask ReleaseAsync(DateTimeOffset now)
        {
            var startDisposal = false;
            lock (_gate)
            {
                if (_activeLeases <= 0)
                {
                    throw new InvalidOperationException(
                        $"Profile runtime {key} received an unmatched lease release.");
                }

                _activeLeases--;
                _lastUsedAt = now;
                if (_retiring && _activeLeases == 0 && !_disposalStarted)
                {
                    _disposalStarted = true;
                    startDisposal = true;
                }
            }

            if (startDisposal)
            {
                await DisposeScopeAsync().ConfigureAwait(false);
            }
        }

        internal async Task RetireAsync()
        {
            var startDisposal = false;
            lock (_gate)
            {
                _retiring = true;
                if (_activeLeases == 0 && !_disposalStarted)
                {
                    _disposalStarted = true;
                    startDisposal = true;
                }
            }

            if (startDisposal)
            {
                await DisposeScopeAsync().ConfigureAwait(false);
            }
            else
            {
                await _disposed.Task.ConfigureAwait(false);
            }
        }

        internal async Task ForceDisposeAsync()
        {
            var startDisposal = false;
            lock (_gate)
            {
                _retiring = true;
                if (!_disposalStarted)
                {
                    _disposalStarted = true;
                    startDisposal = true;
                }
            }

            if (startDisposal)
            {
                await DisposeScopeAsync().ConfigureAwait(false);
            }
            else
            {
                await _disposed.Task.ConfigureAwait(false);
            }
        }

        private async Task DisposeScopeAsync()
        {
            try
            {
                await Scope.DisposeAsync().ConfigureAwait(false);
                _disposed.TrySetResult(true);
            }
            catch (Exception exception)
            {
                ProfileRuntimeTelemetry.DisposalFailures.Add(1);
                RuntimeDisposalFailed(
                    logger,
                    key.ImplementationFingerprint,
                    key.ImplementationRevision,
                    exception);
                _disposed.TrySetException(exception);
                throw;
            }
        }
    }
}
