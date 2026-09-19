namespace SquiFlow.DbMigrator;

public sealed class MigrationLockUnavailableException : TimeoutException
{
    public MigrationLockUnavailableException(TimeSpan timeout)
        : base($"The database migration lock was not acquired within {timeout.TotalSeconds:F0} seconds.")
    {
    }
}
