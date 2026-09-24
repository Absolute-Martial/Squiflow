using Microsoft.EntityFrameworkCore;
using Application.DatabaseMigrator;
using Application.IdentityAccess.Postgres;
using Application.Orders.Postgres;
using Application.Tenancy.Postgres;
using Xunit;

namespace Application.IdentityAccess.Postgres.Tests;

public sealed class MigrationRegistryTests
{
    [Fact]
    public void EveryMigrationOwningPostgresProjectIsRegisteredExactlyOnceInDependencyOrder()
    {
        var root = FindRepositoryRoot();
        var migrationProjects = Directory.EnumerateFiles(
                Path.Combine(root, "modules"), "*DbContextModelSnapshot.cs", SearchOption.AllDirectories)
            .Select(path => Directory.GetParent(Path.GetDirectoryName(path)!)!.FullName)
            .SelectMany(path => Directory.EnumerateFiles(path, "*.csproj", SearchOption.TopDirectoryOnly))
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var registeredProjects = MigrationModules.All
            .Select(module => module.DbContextType.Assembly.GetName().Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(migrationProjects, registeredProjects);
        Assert.Equal(
            [typeof(IdentityAccessDbContext), typeof(TenancyDbContext), typeof(OrderDbContext)],
            MigrationModules.All.Select(module => module.DbContextType));
        Assert.Equal(["identity-access", "tenancy", "orders"], MigrationModules.All.Select(module => module.Name));
        Assert.Equal([100, 200, 300], MigrationModules.All.Select(module => module.Order));

        var solution = File.ReadAllText(Path.Combine(root, "Application.slnx"));
        foreach (var project in migrationProjects)
        {
            Assert.Contains($"{project}.csproj", solution, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MigrationIdsAreUniqueAcrossRegisteredModules()
    {
        var migrations = MigrationModules.All.SelectMany(module =>
        {
            using var database = module.CreateContext("Host=localhost;Database=registry_test");
            return database.Database.GetMigrations();
        }).ToArray();

        Assert.NotEmpty(migrations);
        Assert.Equal(migrations.Length, migrations.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void RegistryRejectsDuplicateNamesOrdersAndContexts()
    {
        var identity = MigrationModules.All[0];
        Assert.Throws<InvalidOperationException>(() => MigrationModules.ValidateAndOrder([identity, identity]));
        Assert.Throws<InvalidOperationException>(() => MigrationModules.ValidateAndOrder(
            [identity, identity with { Name = "other", DbContextType = typeof(TenancyDbContext) }]));
        Assert.Throws<InvalidOperationException>(() => MigrationModules.ValidateAndOrder(
            [identity, identity with { Name = "other", Order = 200 }]));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Application.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Cannot locate Application.slnx from the test output directory.");
    }
}
