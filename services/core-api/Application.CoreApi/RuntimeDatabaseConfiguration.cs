using Npgsql;

namespace Application.CoreApi;

internal sealed class RuntimeDatabaseConfiguration
{
    private readonly string _connectionString;

    internal const string SectionName = "Database";
    internal const string DirectConnectionMode = "Direct";
    internal const string PoolName = "Application.CoreApi.PrimaryDatabase";

    private RuntimeDatabaseConfiguration(
        string connectionString,
        int maximumPoolSize,
        int minimumPoolSize,
        int connectionIdleLifetimeSeconds,
        int connectionPruningIntervalSeconds,
        int connectionLifetimeSeconds)
    {
        _connectionString = connectionString;
        MaximumPoolSize = maximumPoolSize;
        MinimumPoolSize = minimumPoolSize;
        ConnectionIdleLifetimeSeconds = connectionIdleLifetimeSeconds;
        ConnectionPruningIntervalSeconds = connectionPruningIntervalSeconds;
        ConnectionLifetimeSeconds = connectionLifetimeSeconds;
    }

    internal int MaximumPoolSize { get; }

    internal int MinimumPoolSize { get; }

    internal int ConnectionIdleLifetimeSeconds { get; }

    internal int ConnectionPruningIntervalSeconds { get; }

    internal int ConnectionLifetimeSeconds { get; }

    internal static RuntimeDatabaseConfiguration From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("PrimaryDatabase");
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var source = new NpgsqlConnectionStringBuilder(connectionString);
        if (!source.Pooling)
        {
            throw new InvalidOperationException(
                "ConnectionStrings:PrimaryDatabase must enable Npgsql pooling for direct database mode.");
        }

        if (source.NoResetOnClose)
        {
            throw new InvalidOperationException(
                "ConnectionStrings:PrimaryDatabase cannot disable pooled-connection state reset.");
        }

        if (source.Multiplexing)
        {
            throw new InvalidOperationException(
                "ConnectionStrings:PrimaryDatabase cannot enable unqualified Npgsql multiplexing.");
        }

        if (source.LogParameters || source.IncludeErrorDetail || source.IncludeFailedBatchedCommand)
        {
            throw new InvalidOperationException(
                "ConnectionStrings:PrimaryDatabase cannot enable diagnostics that may disclose SQL parameters, failed commands, or provider error details.");
        }

        if (source.PersistSecurityInfo)
        {
            throw new InvalidOperationException(
                "ConnectionStrings:PrimaryDatabase cannot retain security-sensitive connection information after use.");
        }

        var section = configuration.GetRequiredSection(SectionName);
        var connectionMode = section["ConnectionMode"];
        if (!string.Equals(connectionMode, DirectConnectionMode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{SectionName}:ConnectionMode must be '{DirectConnectionMode}' until an external pooler mode passes its compatibility and operations gate.");
        }

        var maximumPoolSize = ReadNonNegativeInt(section, "MaximumPoolSize");
        var minimumPoolSize = ReadNonNegativeInt(section, "MinimumPoolSize");
        var idleLifetime = ReadNonNegativeInt(section, "ConnectionIdleLifetimeSeconds");
        var pruningInterval = ReadNonNegativeInt(section, "ConnectionPruningIntervalSeconds");
        var connectionLifetime = ReadNonNegativeInt(section, "ConnectionLifetimeSeconds");

        if (maximumPoolSize == 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:MaximumPoolSize must be greater than zero.");
        }

        if (minimumPoolSize > maximumPoolSize)
        {
            throw new InvalidOperationException(
                $"{SectionName}:MinimumPoolSize cannot exceed MaximumPoolSize.");
        }

        if (pruningInterval == 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:ConnectionPruningIntervalSeconds must be greater than zero.");
        }

        return new RuntimeDatabaseConfiguration(
            source.ConnectionString,
            maximumPoolSize,
            minimumPoolSize,
            idleLifetime,
            pruningInterval,
            connectionLifetime);
    }

    internal NpgsqlDataSource CreateDataSource(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var connection = new NpgsqlConnectionStringBuilder(_connectionString)
        {
            Pooling = true,
            MaxPoolSize = MaximumPoolSize,
            MinPoolSize = MinimumPoolSize,
            ConnectionIdleLifetime = ConnectionIdleLifetimeSeconds,
            ConnectionPruningInterval = ConnectionPruningIntervalSeconds,
            ConnectionLifetime = ConnectionLifetimeSeconds,
            NoResetOnClose = false,
            Multiplexing = false,
            LogParameters = false,
            IncludeErrorDetail = false,
            IncludeFailedBatchedCommand = false,
            PersistSecurityInfo = false,
        };
        var builder = new NpgsqlDataSourceBuilder(connection.ConnectionString)
        {
            Name = PoolName,
        };
        builder.UseLoggerFactory(loggerFactory);
        builder.EnableParameterLogging(false);
        return builder.Build();
    }

    private static int ReadNonNegativeInt(IConfigurationSection section, string name)
    {
        var raw = section[name];
        if (!int.TryParse(raw, out var value) || value < 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{name} must be a non-negative integer.");
        }

        return value;
    }
}
