using System.Threading.RateLimiting;

namespace Application.CoreApi.Composition;

internal sealed class TenantAdmissionPartitions(int permitLimit, int maximumPartitions) : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Partition> _partitions = [];
    private bool _disposed;

    internal int RetainedPartitionCount
    {
        get { lock (_gate) return _partitions.Count; }
    }

    internal IDisposable? TryAcquire(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A validated tenant identity is required.", nameof(tenantId));
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_partitions.TryGetValue(tenantId, out var partition))
            {
                if (_partitions.Count >= maximumPartitions)
                    return null;
                partition = new Partition(new ConcurrencyLimiter(new ConcurrencyLimiterOptions
                {
                    PermitLimit = permitLimit,
                    QueueLimit = 0,
                }));
                _partitions.Add(tenantId, partition);
            }
            var lease = partition.Limiter.AttemptAcquire();
            if (!lease.IsAcquired)
            {
                lease.Dispose();
                return null;
            }
            partition.ActiveLeases++;
            return new TenantLease(this, tenantId, partition, lease);
        }
    }

    private void Release(Guid tenantId, Partition partition, RateLimitLease lease)
    {
        lock (_gate)
        {
            lease.Dispose();
            if (--partition.ActiveLeases == 0 && !_disposed)
            {
                _partitions.Remove(tenantId);
                partition.Limiter.Dispose();
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var partition in _partitions.Values)
                partition.Limiter.Dispose();
            _partitions.Clear();
        }
    }

    private sealed class Partition(ConcurrencyLimiter limiter)
    {
        internal ConcurrencyLimiter Limiter { get; } = limiter;
        internal int ActiveLeases { get; set; }
    }

    private sealed class TenantLease(TenantAdmissionPartitions owner, Guid tenantId, Partition partition, RateLimitLease lease) : IDisposable
    {
        private TenantAdmissionPartitions? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release(tenantId, partition, lease);
    }
}

// Request DI owns release even when endpoint execution throws or is cancelled.
internal sealed class TenantRequestAdmission(TenantAdmissionPartitions partitions) : IDisposable
{
    private Guid _tenantId;
    private IDisposable? _lease;

    internal bool TryEnter(Guid tenantId)
    {
        if (_lease is not null)
        {
            if (_tenantId != tenantId)
                throw new InvalidOperationException("A request cannot enter multiple tenant boundaries.");
            return true;
        }
        _lease = partitions.TryAcquire(tenantId);
        _tenantId = tenantId;
        return _lease is not null;
    }

    public void Dispose() => Interlocked.Exchange(ref _lease, null)?.Dispose();
}
