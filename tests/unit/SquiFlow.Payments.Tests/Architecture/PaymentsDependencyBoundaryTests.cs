using System.Xml.Linq;
using Xunit;

namespace SquiFlow.Payments.Tests.Architecture;

public sealed class PaymentsDependencyBoundaryTests
{
    [Fact]
    public void Capability_has_no_outward_project_or_package_dependencies()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(
            repositoryRoot.FullName,
            "modules",
            "payments",
            "SquiFlow.Payments",
            "SquiFlow.Payments.csproj");

        var project = XDocument.Load(projectPath);

        var projectReferences = project
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .ToArray();
        var packageReferences = project
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .ToArray();

        Assert.Empty(projectReferences);
        Assert.Empty(packageReferences);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "SquiFlow.sln")))
            {
                return current;
            }
        }

        throw new InvalidOperationException(
            "Could not locate the SquiFlow repository root from the test output directory.");
    }
}
