namespace SquiFlow.Guard.Supervision;

public sealed class RestartBudget
{
    private readonly int _maximumRestarts;
    private readonly TimeSpan _window;
    private readonly Queue<DateTimeOffset> _attempts = new();

    public RestartBudget(int maximumRestarts, TimeSpan window)
    {
        if (maximumRestarts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRestarts));
        }

        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window));
        }

        _maximumRestarts = maximumRestarts;
        _window = window;
    }

    public bool TryRegister(DateTimeOffset now)
    {
        while (_attempts.Count > 0 && now - _attempts.Peek() >= _window)
        {
            _attempts.Dequeue();
        }

        if (_attempts.Count >= _maximumRestarts)
        {
            return false;
        }

        _attempts.Enqueue(now);
        return true;
    }
}
