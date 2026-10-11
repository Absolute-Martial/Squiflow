namespace Application.CoreApi.ImportExecution;

// HTTP acceptance must require this state, rather than merely resolving the batch runner.
// Startup is fail closed until the first successful privileged discovery; drain disables it.
internal sealed class CustomerImportExecutionState
{
    private int _state; // 0 unavailable, 1 accepting, 2 terminal drain
    internal bool IsAcceptingWork => Volatile.Read(ref _state) == 1;
    internal void SetAccepting(bool accepting) => Interlocked.CompareExchange(ref _state, accepting ? 1 : 0, accepting ? 0 : 1);
    internal void BeginDrain() => Interlocked.Exchange(ref _state, 2);
}
