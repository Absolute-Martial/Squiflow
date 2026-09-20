using Xunit;

namespace Application.Tenancy.Tests;

public sealed class TenantContextTests
{
    [Fact]
    public async Task ResolverCreatesContextOnlyFromCurrentMembership()
    {
        var accountId = Guid.NewGuid();
        var allowedTenantId = Guid.NewGuid();
        var deniedTenantId = Guid.NewGuid();
        var resolver = new ResolveTenantContext(
            new FixedMembershipDirectory(accountId, allowedTenantId));

        var allowed = await resolver.ExecuteAsync(accountId, allowedTenantId, CancellationToken.None);
        var denied = await resolver.ExecuteAsync(accountId, deniedTenantId, CancellationToken.None);

        Assert.NotNull(allowed);
        Assert.Equal(allowedTenantId, allowed.TenantId);
        Assert.Equal(accountId, allowed.AccountId);
        Assert.Null(denied);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ResolverRejectsEmptyAuthorityIdentifiers(bool emptyAccount, bool emptyTenant)
    {
        var resolver = new ResolveTenantContext(new FixedMembershipDirectory(Guid.NewGuid(), Guid.NewGuid()));

        await Assert.ThrowsAsync<ArgumentException>(() => resolver.ExecuteAsync(
            emptyAccount ? Guid.Empty : Guid.NewGuid(),
            emptyTenant ? Guid.Empty : Guid.NewGuid(),
            CancellationToken.None));
    }

    [Fact]
    public void CapabilityRemainsHostProviderAndAuthorizationNeutral()
    {
        var references = typeof(TenantContext).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Npgsql", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("OpenFga", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, name => name.StartsWith("FSH", StringComparison.Ordinal));
    }

    private sealed class FixedMembershipDirectory(Guid accountId, Guid tenantId)
        : ITenantMembershipDirectory
    {
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(
            Guid requestedAccountId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<bool> IsActiveAsync(
            Guid requestedAccountId,
            Guid requestedTenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult(requestedAccountId == accountId && requestedTenantId == tenantId);
    }
}
