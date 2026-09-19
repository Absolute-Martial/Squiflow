namespace SquiFlow.DbMigrator;

internal enum MigratorVerb
{
    Apply,
    ListPending,
    Help,
}

internal static class MigratorCommand
{
    public const string HelpText = """
        Application database migrator

        Usage:
          SquiFlow.DbMigrator apply
          SquiFlow.DbMigrator list-pending

        Configuration:
          ConnectionStrings__PrimaryDatabase  required elevated-DDL PostgreSQL connection
          Migration__LockTimeoutSeconds     optional, 1-300 seconds (default 30)

        Exit codes:
          0  success
          1  migration/database failure
          2  invalid command or configuration
          3  another migrator held the advisory lock past the configured timeout
        """;

    public static MigratorVerb Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0 || string.Equals(args[0], "apply", StringComparison.OrdinalIgnoreCase))
        {
            return MigratorVerb.Apply;
        }

        if (string.Equals(args[0], "list-pending", StringComparison.OrdinalIgnoreCase))
        {
            return MigratorVerb.ListPending;
        }

        if (args[0] is "-h" or "--help" || string.Equals(args[0], "help", StringComparison.OrdinalIgnoreCase))
        {
            return MigratorVerb.Help;
        }

        throw new ArgumentException($"Unknown migrator command '{args[0]}'.");
    }
}
