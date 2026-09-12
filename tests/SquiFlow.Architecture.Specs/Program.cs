using System.Xml.Linq;

var repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
var failures = new List<string>();

var neutralProjects = Directory
    .EnumerateFiles(Path.Combine(repoRoot, "foundation", "application-kernel"), "*.csproj", SearchOption.AllDirectories)
    .Concat(Directory.EnumerateFiles(Path.Combine(repoRoot, "modules", "customers", "SquiFlow.Customers"), "*.csproj", SearchOption.AllDirectories))
    .ToArray();

var forbiddenPackages = new[]
{
    "Avalonia",
    "Microsoft.AspNetCore",
    "Microsoft.EntityFrameworkCore",
    "Npgsql",
    "Microsoft.Data.Sqlite",
    "OpenFGA",
    "Zitadel",
    "Quartz",
    "Proto.Actor",
    "MassTransit",
    "RabbitMQ"
};

foreach (var project in neutralProjects)
{
    var document = XDocument.Load(project);
    var packages = document.Descendants("PackageReference")
        .Select(element => (string?)element.Attribute("Include"))
        .Where(value => value is not null)
        .Cast<string>();

    foreach (var package in packages)
    {
        if (forbiddenPackages.Any(prefix => package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add($"{Relative(project)}: forbidden host/provider package '{package}' in shared code.");
        }
    }
}

var sharedSourceRoots = new[]
{
    Path.Combine(repoRoot, "foundation", "application-kernel"),
    Path.Combine(repoRoot, "modules", "customers", "SquiFlow.Customers")
};

var forbiddenNamespaces = new[]
{
    "using Avalonia",
    "using Microsoft.AspNetCore",
    "using Microsoft.EntityFrameworkCore",
    "using Npgsql",
    "using Microsoft.Data.Sqlite",
    "using Quartz",
    "using Proto"
};

foreach (var sourceRoot in sharedSourceRoots)
{
    foreach (var source in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
    {
        var text = File.ReadAllText(source);
        foreach (var forbidden in forbiddenNamespaces)
        {
            if (text.Contains(forbidden, StringComparison.Ordinal))
            {
                failures.Add($"{Relative(source)}: shared code contains '{forbidden}'.");
            }
        }
    }
}

if (Directory.Exists(Path.Combine(repoRoot, "services", "worker")))
{
    failures.Add("services/worker exists before Phase 6 first durable workload.");
}

if (failures.Count == 0)
{
    Console.WriteLine("PASS platform/provider boundaries remain outside shared kernel/module code.");
    return 0;
}

foreach (var failure in failures)
{
    Console.Error.WriteLine($"FAIL {failure}");
}

return 1;

string Relative(string path) => Path.GetRelativePath(repoRoot, path).Replace('\\', '/');

static string FindRepositoryRoot(string start)
{
    var current = new DirectoryInfo(start);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "Directory.Build.props")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new InvalidOperationException("Unable to locate repository root.");
}
