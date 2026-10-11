using Application.Customers;
using Npgsql;
using Xunit;

namespace Application.Customers.Postgres.Tests;

public sealed partial class CustomerPostgresTests
{
    [Fact]
    public async Task ExactDuplicateEvidenceIsNotCrowdedOutByANewerFuzzyPrefixAtLimitOne()
    {
        await MigrateAsync(); var (tenant, account) = await SeedAsync(); var context = await ResolveContextAsync(tenant, account);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync()); var store = new PostgresCustomerStore(source);
        var exact = (await new CreateCustomerIndividual(store).ExecuteAsync(context, new("Other Name", "match@example.test", null), "exact", default)).Individual!;
        await new CreateCustomerIndividual(store).ExecuteAsync(context, new("Prefix Completely Different", "other@example.test", null), "prefix", default);
        var result = await new FindCustomerDuplicates(store).ExecuteAsync(context,
            new(CustomerDuplicateSignals.Create("Prefix Target", "match@example.test", null), Limit: 1, IncludeFuzzyNameDiscovery: true), default);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(exact.IndividualId, candidate.Customer.IndividualId);
        Assert.Contains(candidate.Evidence, evidence => evidence.Kind == CustomerDuplicateEvidenceKind.NormalizedEmail);
    }

}
