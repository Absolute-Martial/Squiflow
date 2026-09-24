using System.Xml.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Application.Architecture.Tests;

public sealed class ProjectBoundariesTests
{
    [Fact]
    public void CurrentProjectsRespectDependencyAndProviderBoundaries()
    {
        var root = FindRepositoryRoot();
        var projects = LoadProjects(root).ToArray();
        var violations = ProjectBoundaries.Check(projects);

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void EveryProjectIsIncludedExactlyOnceInTheSolution()
    {
        var root = FindRepositoryRoot();
        var actual = LoadProjects(root).Select(project => project.Path).ToArray();
        var listed = XDocument.Load(Path.Combine(root, "Application.slnx"))
            .Descendants("Project")
            .Select(element => Path.GetFullPath(Path.Combine(root, (string)element.Attribute("Path")!)))
            .ToArray();

        Assert.Equal(actual.Order(StringComparer.Ordinal), listed.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void CurrentInventoryCountsMatchTheProjectTree()
    {
        var root = FindRepositoryRoot();
        var projects = LoadProjects(root).ToArray();
        var inventory = File.ReadAllText(Path.Combine(root, "README.IMPLEMENTATION.md"));

        Assert.Equal(projects.Count(project => project.Kind != ProjectKind.Test), ReadCount(inventory, "production projects"));
        Assert.Equal(projects.Count(project => project.Kind == ProjectKind.Test), ReadCount(inventory, "test projects"));
        Assert.Equal(projects.Count(project => project.Kind == ProjectKind.Executable), ReadCount(inventory, "executable hosts"));
    }

    [Fact]
    public void CoreApiCannotDirectlyApplySchemaMigrations()
    {
        var root = FindRepositoryRoot();
        var coreApi = Path.Combine(root, "services", "core-api", "Application.CoreApi");
        var sources = Directory.EnumerateFiles(coreApi, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar)
                .Any(part => part is "bin" or "obj"));

        Assert.DoesNotContain(sources, path => Regex.IsMatch(
            File.ReadAllText(path),
            @"\bMigrate(?:Async)?\s*\(",
            RegexOptions.CultureInvariant));
    }

    [Fact]
    public void InvalidReferencesAndProviderPackagesAreRejected()
    {
        const string root = "/repository";
        var core = new ProjectNode(
            Path.Combine(root, "modules/orders/Application.Orders/Application.Orders.csproj"),
            ProjectKind.Capability,
            "orders",
            [Path.Combine(root, "modules/orders/Application.Orders.Postgres/Application.Orders.Postgres.csproj")],
            ["Npgsql"],
            "Microsoft.NET.Sdk",
            null);
        var adapter = new ProjectNode(
            Path.Combine(root, "modules/orders/Application.Orders.Postgres/Application.Orders.Postgres.csproj"),
            ProjectKind.PostgresAdapter,
            "orders",
            [Path.Combine(root, "services/core-api/Application.CoreApi/Application.CoreApi.csproj")],
            [],
            "Microsoft.NET.Sdk",
            null);
        var host = new ProjectNode(
            Path.Combine(root, "services/core-api/Application.CoreApi/Application.CoreApi.csproj"),
            ProjectKind.Executable,
            "core-api",
            [],
            [],
            "Microsoft.NET.Sdk.Web",
            null);

        var violations = ProjectBoundaries.Check([core, adapter, host]);

        Assert.Contains(violations, violation => violation.Contains("cannot reference adapter", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Contains("cannot reference executable", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Contains("provider package Npgsql", StringComparison.Ordinal));
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

        throw new InvalidOperationException("Application.slnx was not found above the test output directory.");
    }

    private static int ReadCount(string inventory, string label)
    {
        var match = Regex.Match(inventory, $"(?m)^{Regex.Escape(label)}:\\s*(?<count>\\d+)\\s*$", RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"README.IMPLEMENTATION.md is missing the {label} inventory count.");
        return int.Parse(match.Groups["count"].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static IEnumerable<ProjectNode> LoadProjects(string root)
    {
        foreach (var area in new[] { "modules", "services", "tests" })
        {
            foreach (var path in Directory.EnumerateFiles(Path.Combine(root, area), "*.csproj", SearchOption.AllDirectories)
                         .Where(path => !path.Split(Path.DirectorySeparatorChar).Contains("obj", StringComparer.Ordinal)))
            {
                var relative = Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar);
                var kind = area switch
                {
                    "modules" when Path.GetFileNameWithoutExtension(path).EndsWith(".Postgres", StringComparison.Ordinal)
                        => ProjectKind.PostgresAdapter,
                    "modules" => ProjectKind.Capability,
                    "services" => ProjectKind.Executable,
                    _ => ProjectKind.Test
                };
                var document = XDocument.Load(path);
                var references = document.Descendants("ProjectReference")
                    .Select(element => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, (string)element.Attribute("Include")!)))
                    .ToArray();
                var packages = document.Descendants("PackageReference")
                    .Select(element => (string)element.Attribute("Include")!)
                    .ToArray();

                yield return new ProjectNode(
                    Path.GetFullPath(path),
                    kind,
                    relative[1],
                    references,
                    packages,
                    (string?)document.Root?.Attribute("Sdk"),
                    document.Descendants("OutputType").Select(element => element.Value).FirstOrDefault());
            }
        }
    }
}

internal enum ProjectKind { Capability, PostgresAdapter, Executable, Test }

internal sealed record ProjectNode(
    string Path,
    ProjectKind Kind,
    string Owner,
    IReadOnlyList<string> References,
    IReadOnlyList<string> Packages,
    string? Sdk,
    string? OutputType);

internal static class ProjectBoundaries
{
    private static readonly string[] ForbiddenCapabilityPackages =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "OpenFga",
        "Finbuckle.MultiTenant.AspNetCore",
        "Autofac",
        "Avalonia"
    ];

    internal static IReadOnlyList<string> Check(IReadOnlyCollection<ProjectNode> projects)
    {
        var violations = new List<string>();
        var byPath = projects.ToDictionary(project => project.Path, StringComparer.Ordinal);

        foreach (var project in projects)
        {
            var name = Path.GetFileNameWithoutExtension(project.Path);
            var directoryName = Path.GetFileName(Path.GetDirectoryName(project.Path));
            if (!name.StartsWith("Application.", StringComparison.Ordinal) ||
                !string.Equals(name, directoryName, StringComparison.Ordinal))
            {
                violations.Add($"{project.Path}: project identity must be a matching Application.* directory and filename.");
            }

            if (project.Kind == ProjectKind.Executable &&
                project.Sdk != "Microsoft.NET.Sdk.Web" && project.OutputType != "Exe")
            {
                violations.Add($"{project.Path}: service project needs an explicit executable classification.");
            }

            if (project.Kind == ProjectKind.Capability)
            {
                if (project.Sdk != "Microsoft.NET.Sdk")
                {
                    violations.Add($"{project.Path}: host-neutral capability must use the plain .NET SDK.");
                }

                foreach (var package in project.Packages)
                {
                    if (ForbiddenCapabilityPackages.Any(prefix =>
                            package.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                            package.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase)))
                    {
                        violations.Add($"{project.Path}: host-neutral capability cannot use provider package {package}.");
                    }
                }
            }

            if (project.Kind == ProjectKind.PostgresAdapter &&
                !project.References.Any(reference => byPath.TryGetValue(reference, out var target) &&
                    target.Kind == ProjectKind.Capability && target.Owner == project.Owner))
            {
                violations.Add($"{project.Path}: PostgreSQL adapter must reference its owning host-neutral capability.");
            }

            foreach (var reference in project.References)
            {
                if (!byPath.TryGetValue(reference, out var target))
                {
                    violations.Add($"{project.Path}: project reference is missing from the repository: {reference}.");
                    continue;
                }

                if (project.Kind == ProjectKind.Capability && target.Kind == ProjectKind.PostgresAdapter)
                {
                    violations.Add($"{project.Path}: host-neutral capability cannot reference adapter {target.Path}.");
                }
                if ((project.Kind is ProjectKind.Capability or ProjectKind.PostgresAdapter or ProjectKind.Executable) &&
                    target.Kind == ProjectKind.Executable)
                {
                    violations.Add($"{project.Path}: cannot reference executable {target.Path}.");
                }
                if (project.Kind == ProjectKind.PostgresAdapter && target.Kind == ProjectKind.PostgresAdapter)
                {
                    violations.Add($"{project.Path}: adapter cannot reference another adapter {target.Path}.");
                }
                if (project.Kind != ProjectKind.Test && target.Kind == ProjectKind.Test)
                {
                    violations.Add($"{project.Path}: production project cannot reference test project {target.Path}.");
                }
            }
        }

        return violations;
    }
}
