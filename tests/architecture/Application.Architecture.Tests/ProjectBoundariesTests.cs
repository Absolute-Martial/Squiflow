using System.Xml.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Application.Architecture.Tests;

public sealed class ProjectBoundariesTests
{
    private static readonly string[] ProjectAreas = ["foundation", "modules", "services", "operations", "apps", "tests"];
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
    public void ProductVersionMarkersMatchTheLockedProductVersion()
    {
        var root = FindRepositoryRoot();
        var buildProperties = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        var lockedVersion = buildProperties.Root?
            .Element("PropertyGroup")?
            .Element("LockedProductVersion")?
            .Value;

        Assert.False(string.IsNullOrWhiteSpace(lockedVersion));

        var expected = $"v{lockedVersion}";
        Assert.Equal(expected, File.ReadAllText(Path.Combine(root, "VERSION")).Trim());
        Assert.Equal(expected, File.ReadAllText(Path.Combine(root, "CURRENT_VERSION.txt")).Trim());
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
            [],
            "Microsoft.NET.Sdk",
            null);
        var adapter = new ProjectNode(
            Path.Combine(root, "modules/orders/Application.Orders.Postgres/Application.Orders.Postgres.csproj"),
            ProjectKind.PostgresAdapter,
            "orders",
            [Path.Combine(root, "services/core-api/Application.CoreApi/Application.CoreApi.csproj")],
            [],
            [],
            "Microsoft.NET.Sdk",
            null);
        var host = new ProjectNode(
            Path.Combine(root, "services/core-api/Application.CoreApi/Application.CoreApi.csproj"),
            ProjectKind.Executable,
            "core-api",
            [],
            [],
            [],
            "Microsoft.NET.Sdk.Web",
            null);

        var violations = ProjectBoundaries.Check([core, adapter, host]);

        Assert.Contains(violations, violation => violation.Contains("cannot reference adapter", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Contains("cannot reference executable", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Contains("provider package Npgsql", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore.App")]
    [InlineData("Microsoft.WindowsDesktop.App")]
    [InlineData("Microsoft.WindowsDesktop.App.WPF")]
    [InlineData("Microsoft.WindowsDesktop.App.WindowsForms")]
    [InlineData("microsoft.aspnetcore.app")]
    public void HostNeutralCapabilityCannotReferenceHostFramework(string frameworkReference)
    {
        const string root = "/repository";
        var capability = new ProjectNode(
            Path.Combine(root, "modules/orders/Application.Orders/Application.Orders.csproj"),
            ProjectKind.Capability,
            "orders",
            [],
            [],
            [frameworkReference],
            "Microsoft.NET.Sdk",
            null);

        var violations = ProjectBoundaries.Check([capability]);

        Assert.Contains(violations, violation =>
            violation.Contains($"host-neutral capability cannot use framework reference {frameworkReference}", StringComparison.Ordinal));
        Assert.Empty(ProjectBoundaries.Check([capability with { FrameworkReferences = [] }]));
    }

    [Fact]
    public void ActiveSourceAndBuildIdentitiesContainNoDevelopmentCodename()
    {
        var root = FindRepositoryRoot();
        var codename = string.Concat("Squi", "Flow");
        var areas = ProjectAreas
            .Select(area => Path.Combine(root, area)).Where(Directory.Exists).ToArray();
        var identities = areas.SelectMany(area => Directory.EnumerateDirectories(area, "*", SearchOption.AllDirectories))
            .Select(Path.GetFileName)
            .Concat(areas.SelectMany(area => Directory.EnumerateFiles(area, "*.csproj", SearchOption.AllDirectories))
                .Select(Path.GetFileName)).Append("Application.slnx");
        Assert.DoesNotContain(identities, name => name is not null &&
            name.Contains(codename, StringComparison.OrdinalIgnoreCase));

        var files = areas.SelectMany(area => Directory.EnumerateFiles(area, "*", SearchOption.AllDirectories))
            .Where(path => Path.GetExtension(path) is ".cs" or ".csproj" or ".json")
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Append(Path.Combine(root, "Application.slnx"));
        Assert.DoesNotContain(files, path => File.ReadAllText(path)
            .Contains(codename, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApiCannotDependOnWorkstationOrItsHostComponents()
    {
        const string root = "/repository";
        var desktop = new ProjectNode(
            Path.Combine(root, "apps/desktop/workstation/Application.Workstation/Application.Workstation.csproj"),
            ProjectKind.Executable, "apps/desktop/workstation", [], [], [], "Microsoft.NET.Sdk", "WinExe");
        var desktopUi = new ProjectNode(
            Path.Combine(root, "apps/desktop/workstation/Application.Workstation.Ui/Application.Workstation.Ui.csproj"),
            ProjectKind.HostLibrary, "apps/desktop/workstation", [], [], [], "Microsoft.NET.Sdk", null);
        var api = new ProjectNode(
            Path.Combine(root, "services/core-api/Application.CoreApi/Application.CoreApi.csproj"),
            ProjectKind.Executable, "core-api", [desktop.Path, desktopUi.Path], [], [], "Microsoft.NET.Sdk.Web", null);

        var violations = ProjectBoundaries.Check([api, desktop, desktopUi]);
        Assert.Contains(violations, violation => violation.Contains("cannot reference executable", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Contains("cannot reference another host's component", StringComparison.Ordinal));
        Assert.Empty(ProjectBoundaries.Check([api with { References = [] }, desktop, desktopUi]));
    }

    [Fact]
    public void RuntimeSqlRemainsInEmbeddedAdapterResources()
    {
        var root = FindRepositoryRoot();
        foreach (var (capability, folder) in new[]
                 {
                      ("Customers", "customers"), ("Orders", "orders"),
                      ("Catalog", "catalog"), ("Pricing", "pricing"),
                     ("IdentityAccess", "identity-access"), ("Tenancy", "tenancy"),
                 })
        {
            var adapter = Path.Combine(root, "modules", folder, $"Application.{capability}.Postgres");
            var project = XDocument.Load(Path.Combine(adapter, $"Application.{capability}.Postgres.csproj"));
            Assert.Contains(project.Descendants("EmbeddedResource"), resource =>
                (string?)resource.Attribute("Include") == "Sql/*.sql" &&
                (string?)resource.Attribute("LogicalName") == $"Application.{capability}.Postgres.Sql.%(Filename)%(Extension)");
            Assert.NotEmpty(Directory.EnumerateFiles(Path.Combine(adapter, "Sql"), "*.sql"));
            // This guards literal placement, not SQL correctness. Real PostgreSQL tests own that proof.
            foreach (var source in Directory.EnumerateFiles(Path.Combine(adapter, "Persistence"), "*.cs"))
            {
                Assert.False(Regex.IsMatch(File.ReadAllText(source),
                    "\"(?:\"\")?\\s*(?:SELECT|INSERT|UPDATE|DELETE|WITH)\\s+",
                    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
                    $"Runtime SQL must remain in adapter-owned embedded resources: {source}");
            }
        }
    }

    [Fact]
    public void HostManifestCoversEveryCurrentServiceAndTheCiMatrix()
    {
        var root = FindRepositoryRoot();
        var manifest = File.ReadAllLines(Path.Combine(root, "eng", "hosts.tsv"))
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
            .Select(line => line.Split('\t'))
            .ToArray();

        Assert.All(manifest, fields => Assert.Equal(3, fields.Length));
        Assert.Equal(manifest.Length, manifest.Select(fields => fields[0]).Distinct(StringComparer.Ordinal).Count());

        var declaredProjects = manifest
            .Select(fields => Path.GetFullPath(Path.Combine(root, fields[1])))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var currentServiceProjects = LoadProjects(root)
            .Where(project => project.Kind == ProjectKind.Executable &&
                (project.Path.StartsWith(Path.Combine(root, "services") + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                 project.Path.StartsWith(Path.Combine(root, "operations") + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            .Select(project => project.Path)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(currentServiceProjects, declaredProjects);

        foreach (var fields in manifest)
        {
            Assert.True(File.Exists(Path.Combine(root, fields[1])), $"Missing host project {fields[1]}.");
            var tests = fields[2].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.NotEmpty(tests);
            Assert.All(tests, test =>
                Assert.True(File.Exists(Path.Combine(root, test)), $"Missing host verification project {test}."));
        }

        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "verify.yml"));
        var matrix = Regex.Match(workflow, @"(?m)^\s*host:\s*\[(?<hosts>[^\]]+)\]", RegexOptions.CultureInvariant);
        Assert.True(matrix.Success, "Verify workflow is missing the independent host matrix.");
        var matrixHosts = matrix.Groups["hosts"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(manifest.Select(fields => fields[0]).Order(StringComparer.Ordinal), matrixHosts);
    }

    [Fact]
    public void QualityWorkflowsKeepPinnedActionsAndMaterialGates()
    {
        var root = FindRepositoryRoot();
        var verify = File.ReadAllText(Path.Combine(root, ".github", "workflows", "verify.yml"));
        var security = File.ReadAllText(Path.Combine(root, ".github", "workflows", "security.yml"));

        Assert.Contains("COLLECT_COVERAGE: \"1\"", verify, StringComparison.Ordinal);
        Assert.Contains("./eng/mutate-orders.sh", verify, StringComparison.Ordinal);
        Assert.Contains("./eng/audit-dependencies.sh", security, StringComparison.Ordinal);

        foreach (var workflow in new[] { verify, security })
        {
            var uses = Regex.Matches(workflow, @"(?m)^\s*uses:\s*[^@\s]+@(?<revision>[^\s#]+)", RegexOptions.CultureInvariant);
            Assert.NotEmpty(uses);
            Assert.All(uses.Cast<Match>(), match =>
                Assert.Matches("^[0-9a-f]{40}$", match.Groups["revision"].Value));
        }
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
        foreach (var area in ProjectAreas)
        {
            if (!Directory.Exists(Path.Combine(root, area)))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(Path.Combine(root, area), "*.csproj", SearchOption.AllDirectories)
                         .Where(path => !path.Split(Path.DirectorySeparatorChar).Contains("obj", StringComparer.Ordinal)))
            {
                var relative = Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar);
                var document = XDocument.Load(path);
                var sdk = (string?)document.Root?.Attribute("Sdk");
                var outputType = document.Descendants("OutputType").Select(element => element.Value).FirstOrDefault();
                var kind = area switch
                {
                    "modules" when Path.GetFileNameWithoutExtension(path).EndsWith(".Postgres", StringComparison.Ordinal)
                        => ProjectKind.PostgresAdapter,
                    "modules" or "foundation" => ProjectKind.Capability,
                    "services" or "operations" => ProjectKind.Executable,
                    "apps" when sdk == "Microsoft.NET.Sdk.Web" || outputType is "Exe" or "WinExe"
                        => ProjectKind.Executable,
                    "apps" => ProjectKind.HostLibrary,
                    _ => ProjectKind.Test
                };
                var references = document.Descendants("ProjectReference")
                    .Select(element => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, (string)element.Attribute("Include")!)))
                    .ToArray();
                var packages = document.Descendants("PackageReference")
                    .Select(element => (string)element.Attribute("Include")!)
                    .ToArray();
                var frameworkReferences = document.Descendants("FrameworkReference")
                    .Select(element => (string)element.Attribute("Include")!)
                    .ToArray();

                yield return new ProjectNode(
                    Path.GetFullPath(path),
                    kind,
                    area == "apps" ? string.Join("/", relative[..^2]) : relative[1],
                    references,
                    packages,
                    frameworkReferences,
                    sdk,
                    outputType);
            }
        }
    }
}

internal enum ProjectKind { Capability, PostgresAdapter, Executable, HostLibrary, Test }
internal sealed record ProjectNode(
    string Path,
    ProjectKind Kind,
    string Owner,
    IReadOnlyList<string> References,
    IReadOnlyList<string> Packages,
    IReadOnlyList<string> FrameworkReferences,
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

    private static readonly string[] ForbiddenCapabilityFrameworks =
    [
        "Microsoft.AspNetCore.App",
        "Microsoft.WindowsDesktop.App",
        "Microsoft.WindowsDesktop.App.WPF",
        "Microsoft.WindowsDesktop.App.WindowsForms"
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
                project.Sdk != "Microsoft.NET.Sdk.Web" && project.OutputType is not ("Exe" or "WinExe"))
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

                foreach (var framework in project.FrameworkReferences)
                {
                    if (ForbiddenCapabilityFrameworks.Contains(framework, StringComparer.OrdinalIgnoreCase))
                    {
                        violations.Add($"{project.Path}: host-neutral capability cannot use framework reference {framework}.");
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
                if ((project.Kind is ProjectKind.Capability or ProjectKind.PostgresAdapter or ProjectKind.Executable or ProjectKind.HostLibrary) &&
                    target.Kind == ProjectKind.Executable)
                {
                    violations.Add($"{project.Path}: cannot reference executable {target.Path}.");
                }
                if (target.Kind == ProjectKind.HostLibrary &&
                    (project.Kind is ProjectKind.Capability or ProjectKind.PostgresAdapter ||
                     project.Owner != target.Owner && project.Kind is (ProjectKind.Executable or ProjectKind.HostLibrary)))
                {
                    violations.Add($"{project.Path}: cannot reference another host's component {target.Path}.");
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
