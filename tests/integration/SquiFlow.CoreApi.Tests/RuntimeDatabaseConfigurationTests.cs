using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace SquiFlow.CoreApi.Tests;

public sealed class RuntimeDatabaseConfigurationTests
{
    [Fact]
    public void DirectModeBuildsOneBoundedResettingNpgsqlPool()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:PrimaryDatabase"] =
                    "Host=database.example.test;Database=application;Username=runtime;Password=secret;Maximum Pool Size=99",
                ["Database:ConnectionMode"] = "Direct",
                ["Database:MaximumPoolSize"] = "17",
                ["Database:MinimumPoolSize"] = "2",
                ["Database:ConnectionIdleLifetimeSeconds"] = "240",
                ["Database:ConnectionPruningIntervalSeconds"] = "12",
                ["Database:ConnectionLifetimeSeconds"] = "1800",
            });

        var validated = RuntimeDatabaseConfiguration.From(configuration);
        using var loggerFactory = LoggerFactory.Create(_ => { });
        using var dataSource = validated.CreateDataSource(loggerFactory);
        var effective = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);

        Assert.True(effective.Pooling);
        Assert.Equal(17, effective.MaxPoolSize);
        Assert.Equal(2, effective.MinPoolSize);
        Assert.Equal(240, effective.ConnectionIdleLifetime);
        Assert.Equal(12, effective.ConnectionPruningInterval);
        Assert.Equal(1800, effective.ConnectionLifetime);
        Assert.False(effective.NoResetOnClose);
        Assert.False(effective.Multiplexing);
        Assert.False(effective.LogParameters);
        Assert.False(effective.IncludeErrorDetail);
        Assert.False(effective.IncludeFailedBatchedCommand);
        Assert.False(effective.PersistSecurityInfo);
        Assert.DoesNotContain("secret", validated.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Pooling=false", "must enable Npgsql pooling")]
    [InlineData("No Reset On Close=true", "cannot disable pooled-connection state reset")]
    [InlineData("Multiplexing=true", "cannot enable unqualified Npgsql multiplexing")]
    [InlineData("Log Parameters=true", "cannot enable diagnostics")]
    [InlineData("Include Error Detail=true", "cannot enable diagnostics")]
    [InlineData("Include Failed Batched Command=true", "cannot enable diagnostics")]
    [InlineData("Persist Security Info=true", "cannot retain security-sensitive")]
    public void UnsafeOrUnqualifiedDriverModesFailConfiguration(
        string connectionOption,
        string expectedMessage)
    {
        var configuration = CreateValidConfiguration(connectionOption);

        var error = Assert.Throws<InvalidOperationException>(
            () => RuntimeDatabaseConfiguration.From(configuration));

        Assert.Contains(expectedMessage, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExternalPoolerModeFailsUntilItsCompatibilityGateIsQualified()
    {
        var values = ValidValues();
        values["Database:ConnectionMode"] = "TransactionProxy";

        var error = Assert.Throws<InvalidOperationException>(
            () => RuntimeDatabaseConfiguration.From(CreateConfiguration(values)));

        Assert.Contains("external pooler mode passes", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Database:MaximumPoolSize", "0", "must be greater than zero")]
    [InlineData("Database:MinimumPoolSize", "21", "cannot exceed MaximumPoolSize")]
    [InlineData("Database:ConnectionPruningIntervalSeconds", "0", "must be greater than zero")]
    [InlineData("Database:ConnectionIdleLifetimeSeconds", "invalid", "must be a non-negative integer")]
    public void InvalidResourceBoundsFailConfiguration(
        string key,
        string value,
        string expectedMessage)
    {
        var values = ValidValues();
        values[key] = value;

        var error = Assert.Throws<InvalidOperationException>(
            () => RuntimeDatabaseConfiguration.From(CreateConfiguration(values)));

        Assert.Contains(expectedMessage, error.Message, StringComparison.Ordinal);
    }

    private static IConfiguration CreateValidConfiguration(string connectionOption)
    {
        var values = ValidValues();
        values["ConnectionStrings:PrimaryDatabase"] =
            $"Host=database.example.test;Database=application;{connectionOption}";
        return CreateConfiguration(values);
    }

    private static Dictionary<string, string?> ValidValues() =>
        new()
        {
            ["ConnectionStrings:PrimaryDatabase"] =
                "Host=database.example.test;Database=application",
            ["Database:ConnectionMode"] = "Direct",
            ["Database:MaximumPoolSize"] = "20",
            ["Database:MinimumPoolSize"] = "0",
            ["Database:ConnectionIdleLifetimeSeconds"] = "300",
            ["Database:ConnectionPruningIntervalSeconds"] = "10",
            ["Database:ConnectionLifetimeSeconds"] = "3600",
        };

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
