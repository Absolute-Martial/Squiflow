using Xunit;

namespace Application.AdminApi.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AdminApiIntegrationFixtureGroup : ICollectionFixture<AdminApiTestEnvironment>
{
    internal const string Name = "AdminApi integration";
}
