using System.Threading.Channels;
using Application.CoreApi.Authorization;
using Application.CoreApi.ImportExecution;
using Application.Customers;
using Application.IdentityAccess;
using Application.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class CustomerImportExecutorTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task DisabledExecutorCannotDiscoverClaimOrEnableAdmission()
    {
        var fixture = await ExecutorPorts.CreateAsync();
        await using var services = ExecutorServices(fixture);
        var state = new CustomerImportExecutionState();
        using var executor = Executor(services, state, enabled: false);
        try
        {
            await executor.StartAsync(CancellationToken.None);
            await executor.ExecuteTask!.WaitAsync(TestTimeout);
            Assert.False(state.IsAcceptingWork);
            Assert.Equal(0, fixture.Discoveries); Assert.Equal(0, fixture.Claims);
        }
        finally { await StopAsync(executor); }
    }

    [Fact]
    public async Task NewExecutorRediscoversRetainedCandidateWithoutAnHttpTrigger()
    {
        var fixture = await ExecutorPorts.CreateAsync(); fixture.RemainingRows = 2;
        await using var services = ExecutorServices(fixture);
        var firstState = new CustomerImportExecutionState();
        using (var first = Executor(services, firstState))
        {
            try
            {
                await first.StartAsync(CancellationToken.None);
                Assert.Equal(1, await fixture.Rows.Reader.ReadAsync().AsTask().WaitAsync(TestTimeout));
            }
            finally { await StopAsync(first); }
        }
        Assert.False(firstState.IsAcceptingWork); Assert.Equal(1, fixture.RemainingRows);
        var secondState = new CustomerImportExecutionState();
        using var second = Executor(services, secondState);
        try
        {
            await second.StartAsync(CancellationToken.None);
            Assert.Equal(2, await fixture.Rows.Reader.ReadAsync().AsTask().WaitAsync(TestTimeout));
        }
        finally { await StopAsync(second); }
        Assert.False(secondState.IsAcceptingWork);
        Assert.Equal(0, fixture.RemainingRows); Assert.Equal(2, fixture.AuthorityChecks);
        Assert.Equal(1, fixture.MaximumConcurrentClaims); Assert.Equal(2, fixture.Releases);
    }

    [Fact]
    public async Task ShutdownCancelsTheRowAndAwaitsCleanupBeforeDisposingItsScope()
    {
        var fixture = await ExecutorPorts.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Process = async token =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return CustomerImportRowExecutionStatus.Processed;
        };
        fixture.Cleanup = async token =>
        {
            cleanupEntered.TrySetResult();
            await allowCleanup.Task.WaitAsync(token);
        };
        await using var services = ExecutorServices(fixture);
        var state = new CustomerImportExecutionState(); using var executor = Executor(services, state);
        Task? stopping = null;
        try
        {
            await executor.StartAsync(CancellationToken.None); await entered.Task.WaitAsync(TestTimeout);
            Assert.True(state.IsAcceptingWork); Assert.Equal(1, fixture.DisposedScopes);
            stopping = StopAsync(executor);
            await cleanupEntered.Task.WaitAsync(TestTimeout);
            Assert.False(stopping.IsCompleted); Assert.False(state.IsAcceptingWork);
            state.SetAccepting(true); Assert.False(state.IsAcceptingWork); // drain cannot be reopened by a late discovery
            Assert.Equal(1, fixture.DisposedScopes); Assert.Equal(0, fixture.ProcessedRows);
            allowCleanup.TrySetResult(); await stopping;
            Assert.Equal(2, fixture.DisposedScopes); Assert.Equal(1, fixture.Releases);
        }
        finally { allowCleanup.TrySetResult(); await (stopping ?? StopAsync(executor)); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DenialPausesWorkButProviderOutageOnlyDefersItWithoutRowEffects(bool unavailable)
    {
        var fixture = await ExecutorPorts.CreateAsync();
        fixture.AuthorityDenied = !unavailable; fixture.AuthorityUnavailable = unavailable;
        await using var services = ExecutorServices(fixture);
        var state = new CustomerImportExecutionState(); using var executor = Executor(services, state);
        try
        {
            await executor.StartAsync(CancellationToken.None);
            var denied = await fixture.ReleaseDecisions.Reader.ReadAsync().AsTask().WaitAsync(TestTimeout);
            Assert.Equal(!unavailable, denied); Assert.Equal(0, fixture.ProcessedRows);
        }
        finally { await StopAsync(executor); }
    }

    [Fact]
    public async Task FailedDiscoveryKeepsAdmissionClosedAndDoesNotLogProviderPayload()
    {
        var fixture = await ExecutorPorts.CreateAsync();
        fixture.DiscoveryFailure = new NpgsqlException("private-source-and-provider-content");
        var logger = new CaptureLogger();
        await using var services = ExecutorServices(fixture);
        var state = new CustomerImportExecutionState();
        using var executor = new CustomerImportHostedExecutor(services.GetRequiredService<IServiceScopeFactory>(),
            new(true, TimeSpan.FromHours(1), 10, 1, TimeSpan.FromSeconds(20)), state, logger);
        try
        {
            await executor.StartAsync(CancellationToken.None);
            await logger.Logged.Task.WaitAsync(TestTimeout);
            Assert.False(state.IsAcceptingWork); Assert.Equal(0, fixture.Claims);
            Assert.DoesNotContain("private-source", logger.Message, StringComparison.Ordinal);
            Assert.Null(logger.Exception);
        }
        finally { await StopAsync(executor); }
    }

    [Theory]
    [InlineData("account")]
    [InlineData("tenant")]
    [InlineData("membership")]
    public async Task CurrentAuthorityDeniesInactiveAccountTenantOrMembershipBeforeProviderCheck(string unavailable)
    {
        var ports = new AuthorityPorts
        {
            AccountActive = unavailable != "account",
            TenantActive = unavailable != "tenant",
            MemberActive = unavailable != "membership",
        };
        var authority = ports.Adapter();
        Assert.Null(await authority.CheckAsync(ports.TenantId, ports.AccountId, CancellationToken.None));
        Assert.Equal(0, ports.ProviderChecks);
    }

    [Fact]
    public async Task CurrentAuthorityUsesAuthoritativeRevisionAndRechecksFreshFactsOnEveryCall()
    {
        var ports = new AuthorityPorts(); var authority = ports.Adapter();
        var first = await authority.CheckAsync(ports.TenantId, ports.AccountId, CancellationToken.None);
        Assert.Equal(12, first!.AuthorizationRevision); Assert.Equal(ports.TenantId, first.Context.TenantId);
        Assert.Equal(2, ports.RevisionReads);
        ports.AccountActive = false;
        Assert.Null(await authority.CheckAsync(ports.TenantId, ports.AccountId, CancellationToken.None));
        Assert.Equal(2, ports.AccountReads); Assert.Equal(1, ports.ProviderChecks);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task MissingOrUnsupportedAuthoritativeRevisionCannotBecomeAnInventedAllowance(int? revision)
    {
        var ports = new AuthorityPorts { Revision = revision };
        Assert.Null(await ports.Adapter().CheckAsync(ports.TenantId, ports.AccountId, CancellationToken.None));
        Assert.Equal(0, ports.ProviderChecks);
    }

    [Fact]
    public async Task CurrentAuthorityDistinguishesProviderDenyUnavailableRevisionRaceAndCancellation()
    {
        var ports = new AuthorityPorts(); var authority = ports.Adapter();
        ports.ProviderAllowed = false;
        Assert.Null(await authority.CheckAsync(ports.TenantId, ports.AccountId, CancellationToken.None));
        ports.ProviderAllowed = true; ports.ProviderUnavailable = true;
        await Assert.ThrowsAsync<AuthorizationProviderUnavailableException>(() => authority.CheckAsync(ports.TenantId, ports.AccountId, CancellationToken.None));
        ports.ProviderUnavailable = false; ports.OnProvider = () => ports.Revision++;
        Assert.Null(await authority.CheckAsync(ports.TenantId, ports.AccountId, CancellationToken.None));
        using var cancellation = new CancellationTokenSource(); ports.OnProvider = cancellation.Cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => authority.CheckAsync(ports.TenantId, ports.AccountId, cancellation.Token));
    }

    [Theory]
    [InlineData("Enabled", "perhaps")]
    [InlineData("PollIntervalSeconds", "0")]
    [InlineData("PollIntervalSeconds", "301")]
    [InlineData("TenantPageSize", "51")]
    [InlineData("BatchRows", "0")]
    [InlineData("OperationTimeoutSeconds", "61")]
    public void ExecutorConfigurationRejectsInvalidBounds(string name, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { [$"CustomerImports:Execution:{name}"] = value }).Build();
        Assert.Throws<InvalidOperationException>(() => CustomerImportExecutionConfiguration.From(configuration));
    }

    [Fact]
    public void MissingExecutorConfigurationDefaultsToDisabledAndStandardBounds()
    {
        var configuration = CustomerImportExecutionConfiguration.From(new ConfigurationBuilder().Build());
        Assert.False(configuration.Enabled); Assert.Equal(25, configuration.BatchRows);
        Assert.Equal(10, configuration.TenantPageSize); Assert.Equal(TimeSpan.FromSeconds(5), configuration.PollInterval);
        Assert.Equal(TimeSpan.FromSeconds(20), configuration.OperationTimeout);
    }

    [Theory]
    [InlineData(1, "00:00:30", "StopHost", true)]
    [InlineData(2, "00:00:10", "StopHost", true)]
    [InlineData(2, "00:00:30", "Ignore", true)]
    [InlineData(2, "00:00:30", "StopHost", false)]
    public void ExecutorRegistrationRequiresSharedPoolCapacityAndAnAdequateStandardHostDrain(
        int poolSize, string shutdown, string exceptionBehavior, bool invalid)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PrimaryDatabase"] = "Host=localhost;Database=configuration_fixture",
            ["Database:ConnectionMode"] = "Direct",
            ["Database:MaximumPoolSize"] = poolSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Database:MinimumPoolSize"] = "0",
            ["Database:ConnectionIdleLifetimeSeconds"] = "300",
            ["Database:ConnectionPruningIntervalSeconds"] = "10",
            ["Database:ConnectionLifetimeSeconds"] = "3600",
            ["Database:CommandTimeoutSeconds"] = "15",
            ["CustomerImports:Execution:Enabled"] = "true",
            ["HostOptions:ShutdownTimeout"] = shutdown,
            ["HostOptions:BackgroundServiceExceptionBehavior"] = exceptionBehavior,
        }).Build();
        var services = new ServiceCollection();
        if (poolSize == 1)
        {
            Assert.Throws<InvalidOperationException>(() => services.AddCustomerImportExecution(configuration, RuntimeDatabaseConfiguration.From(configuration)));
            return;
        }
        services.AddCustomerImportExecution(configuration, RuntimeDatabaseConfiguration.From(configuration));
        using var provider = services.BuildServiceProvider();
        if (invalid) Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<HostOptions>>().Value);
        else Assert.Equal(TimeSpan.FromSeconds(30), provider.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout);
    }

    private static ServiceProvider ExecutorServices(ExecutorPorts ports)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => new ScopeWitness(ports));
        services.AddScoped<ICustomerImportWorkStore>(provider => { provider.GetRequiredService<ScopeWitness>(); return ports; });
        services.AddScoped<ICustomerImportWorkDiscovery>(provider => { provider.GetRequiredService<ScopeWitness>(); return ports; });
        services.AddSingleton<ICustomerImportAuthority>(ports); services.AddScoped<RunCustomerImportBatch>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static CustomerImportHostedExecutor Executor(ServiceProvider services, CustomerImportExecutionState state, bool enabled = true) =>
        new(services.GetRequiredService<IServiceScopeFactory>(),
            new(enabled, TimeSpan.FromHours(1), 10, 1, TimeSpan.FromSeconds(20)), state,
            NullLogger<CustomerImportHostedExecutor>.Instance);

    private static async Task StopAsync(CustomerImportHostedExecutor executor)
    {
        using var timeout = new CancellationTokenSource(TestTimeout); await executor.StopAsync(timeout.Token);
        if (executor.ExecuteTask is not null) await executor.ExecuteTask.WaitAsync(timeout.Token);
    }

    private sealed class ScopeWitness(ExecutorPorts ports) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() { Interlocked.Increment(ref ports.DisposedScopes); return ValueTask.CompletedTask; }
    }

    private sealed class CaptureLogger : ILogger<CustomerImportHostedExecutor>
    {
        internal readonly TaskCompletionSource Logged = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal string Message = "";
        internal Exception? Exception;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { Message = formatter(state, exception); Exception = exception; Logged.TrySetResult(); }
    }

    private sealed class ExecutorPorts(TenantContext context) : ICustomerImportWorkStore, ICustomerImportWorkDiscovery, ICustomerImportAuthority
    {
        internal int RemainingRows = 1, ProcessedRows, Discoveries, Claims, Releases, AuthorityChecks, DisposedScopes;
        internal int ConcurrentClaims, MaximumConcurrentClaims;
        internal bool AuthorityDenied, AuthorityUnavailable;
        internal Exception? DiscoveryFailure;
        internal Func<CancellationToken, Task<CustomerImportRowExecutionStatus>>? Process;
        internal Func<CancellationToken, Task>? Cleanup;
        internal readonly Channel<int> Rows = Channel.CreateBounded<int>(10);
        internal readonly Channel<bool> ReleaseDecisions = Channel.CreateBounded<bool>(10);

        internal static async Task<ExecutorPorts> CreateAsync()
        {
            var ports = new AuthorityPorts();
            return new((await new ResolveTenantContext(ports).ExecuteAsync(ports.AccountId, ports.TenantId, CancellationToken.None))!);
        }

        public Task<CustomerImportTenantPage> DiscoverRunnableTenantsAsync(Guid? afterTenantId, int limit, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Interlocked.Increment(ref Discoveries);
            if (DiscoveryFailure is not null) throw DiscoveryFailure;
            return Task.FromResult(new CustomerImportTenantPage(RemainingRows > 0 ? [context.TenantId] : [], null));
        }

        public Task<CustomerImportClaim?> ClaimImportAsync(Guid tenantId, Guid workerId, TimeSpan lease, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Interlocked.Increment(ref Claims);
            if (RemainingRows == 0) return Task.FromResult<CustomerImportClaim?>(null);
            MaximumConcurrentClaims = Math.Max(MaximumConcurrentClaims, Interlocked.Increment(ref ConcurrentClaims));
            return Task.FromResult<CustomerImportClaim?>(new(tenantId, Guid.NewGuid(), Guid.NewGuid(), context.AccountId, 12,
                workerId, Claims, DateTimeOffset.UtcNow + lease));
        }

        public async Task<CustomerImportRowExecutionStatus> ProcessNextImportRowAsync(CustomerImportClaim claim, TenantContext current, CancellationToken ct)
        {
            if (Process is not null) return await Process(ct);
            ct.ThrowIfCancellationRequested(); RemainingRows--; ProcessedRows++;
            Rows.Writer.TryWrite(ProcessedRows); return CustomerImportRowExecutionStatus.Processed;
        }

        public async Task ReleaseImportClaimAsync(CustomerImportClaim claim, bool authorityDenied, CancellationToken ct)
        {
            if (Cleanup is not null) await Cleanup(ct);
            Interlocked.Decrement(ref ConcurrentClaims); Interlocked.Increment(ref Releases);
            ReleaseDecisions.Writer.TryWrite(authorityDenied);
        }

        public Task<CustomerImportAuthoritySnapshot?> CheckAsync(Guid tenantId, Guid accountId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Interlocked.Increment(ref AuthorityChecks);
            if (AuthorityUnavailable) throw new AuthorizationProviderUnavailableException("Synthetic unavailable.", new HttpRequestException());
            return Task.FromResult(AuthorityDenied ? null : new CustomerImportAuthoritySnapshot(context, 12));
        }
    }

    private sealed class AuthorityPorts : IAccountDirectory, ITenantDirectory, ITenantMembershipDirectory,
        ITenantCustomerAuthorization, ITenantAuthorizationAdministrationStore
    {
        internal readonly Guid TenantId = Guid.NewGuid(), AccountId = Guid.NewGuid();
        internal bool AccountActive = true, TenantActive = true, MemberActive = true, ProviderAllowed = true, ProviderUnavailable;
        internal int? Revision = 12;
        internal int AccountReads, RevisionReads, ProviderChecks;
        internal Action? OnProvider;
        internal CurrentCustomerImportAuthority Adapter() => new(this, this, new ResolveTenantContext(this), this, this);
        public Task<AccountAvailability?> FindAvailabilityAsync(Guid id, CancellationToken ct)
        { AccountReads++; return Task.FromResult<AccountAvailability?>(AccountActive ? AccountAvailability.Active : AccountAvailability.Disabled); }
        public Task<TenantSnapshot?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult<TenantSnapshot?>(
            new(TenantId, "Fixture", TenantActive ? TenantAvailability.Active : TenantAvailability.Suspended));
        public Task<bool> IsActiveAsync(Guid account, Guid tenant, CancellationToken ct) => Task.FromResult(MemberActive);
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(Guid account, CancellationToken ct) => throw new NotSupportedException();
        public Task<int?> GetAuthorizationRevisionAsync(Guid tenant, CancellationToken ct)
        { RevisionReads++; return Task.FromResult<int?>(Revision); }
        public Task<bool> CanImportCustomersAsync(Guid account, Guid tenant, CancellationToken ct)
        {
            ProviderChecks++; OnProvider?.Invoke();
            if (ProviderUnavailable) throw new AuthorizationProviderUnavailableException("Synthetic unavailable.", new HttpRequestException());
            return Task.FromResult(ProviderAllowed);
        }
        public Task<bool> CanCreateOrganizationAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanViewOrganizationsAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanCreateProgramAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanViewProgramsAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanCreateIndividualAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanViewIndividualsAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanChangeIndividualAvailabilityAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanEditIndividualContactAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanViewRepresentativesAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanManageRepresentativesAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanResolveCustomerDuplicatesAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> CanConsolidateCustomerDuplicatesAsync(Guid a, Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<bool> IsInitialOwnerAsync(Guid t, Guid a, CancellationToken c) => throw new NotSupportedException();
        public Task<TenantAuthorizationProposalResult> ProposeAsync(TenantAuthorizationActor a, TenantAuthorizationProposalIntent i, DateTimeOffset at, CancellationToken c) => throw new NotSupportedException();
        public Task<TenantAuthorizationProposal?> FindProposalAsync(Guid t, Guid p, CancellationToken c) => throw new NotSupportedException();
        public Task<TenantAuthorizationProposal?> MarkAttemptAsync(Guid t, Guid p, DateTimeOffset at, CancellationToken c) => throw new NotSupportedException();
        public Task<TenantAuthorizationProposal?> MarkUncertainAsync(Guid t, Guid p, string f, DateTimeOffset at, CancellationToken c) => throw new NotSupportedException();
        public Task<TenantAuthorizationProposal?> MarkFailedAsync(Guid t, Guid p, string f, DateTimeOffset at, CancellationToken c) => throw new NotSupportedException();
        public Task<TenantAuthorizationProposal?> CompleteAsync(Guid t, Guid p, DateTimeOffset at, CancellationToken c) => throw new NotSupportedException();
        public Task<IReadOnlyList<TenantCustomRole>> ListRolesAsync(Guid t, CancellationToken c) => throw new NotSupportedException();
        public Task<TenantCustomRole?> FindRoleAsync(Guid t, Guid r, CancellationToken c) => throw new NotSupportedException();
        public Task<IReadOnlyList<TenantRoleAssignment>> ListRoleAssignmentsAsync(Guid t, Guid r, CancellationToken c) => throw new NotSupportedException();
        public Task<TenantOwnerTransferResult> TransferInitialOwnerAsync(TenantAuthorizationActor a, TenantOwnerTransferIntent i, DateTimeOffset at, CancellationToken c) => throw new NotSupportedException();
    }
}
