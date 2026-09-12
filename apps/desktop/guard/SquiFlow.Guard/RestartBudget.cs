namespace SquiFlow.Guard;

public sealed class RestartBudget
{
    private readonly Queue<DateTimeOffset> _attempts = new();

    public RestartBudget(int maximumRestarts, TimeSpan window)
    {
        if (maximumRestarts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRestarts));
        }

        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window));
        }

        MaximumRestarts = maximumRestarts;
        Window = window;
    }

    public int MaximumRestarts { get; }
    public TimeSpan Window { get; }

    public bool TryRegister(DateTimeOffset now)
    {
        while (_attempts.Count > 0 && now - _attempts.Peek() > Window)
        {
            _attempts.Dequeue();
        }

        if (_attempts.Count >= MaximumRestarts)
        {
            return false;
        }

        _attempts.Enqueue(now);
        return true;
    }
}
