using Autofac;

namespace Application.CoreApi.Composition;

internal sealed class ProfileRuntimeLease : IAsyncDisposable
{
    private readonly ILifetimeScope _operationScope;
    private readonly Func<ValueTask> _release;
    private int _disposed;

    internal ProfileRuntimeLease(
        ProfileRuntimeKey key,
        ILifetimeScope operationScope,
        Func<ValueTask> release)
    {
        Key = key;
        _operationScope = operationScope;
        _release = release;
    }

    internal ProfileRuntimeKey Key { get; }

    internal T Resolve<T>() where T : notnull
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);

        return _operationScope.Resolve<T>();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await _operationScope.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await _release().ConfigureAwait(false);
        }
    }
}
