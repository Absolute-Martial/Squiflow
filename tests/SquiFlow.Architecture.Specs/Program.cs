using System.Xml.Linq;

var repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
var failures = new List<string>();

var moduleAdapterSuffixes = new[]
{
    ".Workstation",
    ".Web",
    ".Api",
    ".Postgres",
    ".Sqlite"
};

var neutralProjects = Directory
    .EnumerateFiles(Path.Combine(repoRoot, "foundation", "application-kernel"), "*.csproj", SearchOption.AllDirectories)
    .Concat(Directory.Exists(Path.Combine(repoRoot, "modules"))
        ? Directory.EnumerateFiles(Path.Combine(repoRoot, "modules"), "*.csproj", SearchOption.AllDirectories)
            .Where(path =>
            {
                var projectName = Path.GetFileNameWithoutExtension(path);
                return !moduleAdapterSuffixes.Any(suffix => projectName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            })
        : Array.Empty<string>())
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
    "OpenBao",
    "Quartz",
    "TickerQ",
    "Proto.Actor",
    "MassTransit",
    "RabbitMQ"
};

foreach (var project in neutralProjects)
{
    var document = XDocument.Load(project);
    var packages = document.Descendants("PackageReference")
        .Select(element => (string?)element.Attribute("Include"))
        .OfType<string>();

    foreach (var package in packages)
    {
        if (forbiddenPackages.Any(prefix => package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add($"{Relative(project)}: forbidden host/provider package '{package}' in host-neutral code.");
        }
    }
}

var forbiddenNamespaces = new[]
{
    "using Avalonia",
    "using Microsoft.AspNetCore",
    "using Microsoft.EntityFrameworkCore",
    "using Npgsql",
    "using Microsoft.Data.Sqlite",
    "using OpenFGA",
    "using Zitadel",
    "using Quartz",
    "using Proto.Actor",
    "using MassTransit",
    "using RabbitMQ"
};

foreach (var project in neutralProjects)
{
    var sourceRoot = Path.GetDirectoryName(project)!;
    foreach (var source in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
    {
        var text = File.ReadAllText(source);
        foreach (var forbidden in forbiddenNamespaces)
        {
            if (text.Contains(forbidden, StringComparison.Ordinal))
            {
                failures.Add($"{Relative(source)}: host-neutral code contains '{forbidden}'.");
            }
        }
    }
}

var guardProject = Path.Combine(repoRoot, "apps", "desktop", "guard", "SquiFlow.Guard", "SquiFlow.Guard.csproj");
if (File.Exists(guardProject))
{
    var guardText = File.ReadAllText(guardProject);
    if (guardText.Contains("modules/", StringComparison.OrdinalIgnoreCase) ||
        guardText.Contains("modules\\", StringComparison.OrdinalIgnoreCase))
    {
        failures.Add("Guard must not reference business capability projects.");
    }
}

var unearnedExecutables = new[]
{
    Path.Combine("services", "web-api"),
    Path.Combine("services", "sync-api"),
    Path.Combine("services", "admin-api"),
    Path.Combine("services", "worker"),
    Path.Combine("apps", "admin-web"),
    Path.Combine("apps", "desktop", "diagnostics"),
    Path.Combine("apps", "desktop", "maintenance"),
    Path.Combine("apps", "desktop", "sync"),
    Path.Combine("apps", "desktop", "document")
};

foreach (var relativePath in unearnedExecutables)
{
    if (Directory.Exists(Path.Combine(repoRoot, relativePath)))
    {
        failures.Add($"Unearned executable directory exists: {relativePath.Replace('\\', '/')}.");
    }
}

if (File.Exists(Path.Combine(repoRoot, ".gitlab-ci.yml")))
{
    failures.Add(".gitlab-ci.yml is present even though the current documentation-first rewrite explicitly removes repository CI/CD by user direction.");
}

if (failures.Count == 0)
{
    Console.WriteLine("PASS current Phase-0 dependency and repository boundaries.");
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
        if (Directory.Exists(Path.Combine(current.FullName, "docs")) &&
            File.Exists(Path.Combine(current.FullName, "Directory.Build.props")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new InvalidOperationException("Unable to locate repository root.");
}
