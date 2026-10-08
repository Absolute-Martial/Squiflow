using Application.Customers;
using Application.ObjectStorage;
using Application.Tenancy;
using Xunit;

namespace Application.Customers.Tests;

public sealed class CustomerDuplicateAndImportTests
{
    [Theory]
    [InlineData("१२३४५६७")]
    [InlineData("١٢٣٤٥٦٧")]
    [InlineData("123+456")]
    [InlineData("++123456")]
    public void PhoneSignalsRejectUnicodeDigitsAndAmbiguousPlus(string phone)
    {
        Assert.Equal("phone_invalid", Assert.Throws<CustomerValidationException>(() =>
            CustomerDuplicateSignals.Create("Person", null, phone)).Code);
    }

    [Fact]
    public void ReasonIsBoundedNormalizedAndCannotCarryControlCharacters()
    {
        Assert.Equal("Keep apart", CustomerIdentityNormalization.Reason(" Keep apart "));
        Assert.Equal("reason_invalid", Assert.Throws<CustomerValidationException>(() =>
            CustomerIdentityNormalization.Reason(new string('x', 1001))).Code);
        Assert.Equal("reason_invalid", Assert.Throws<CustomerValidationException>(() =>
            CustomerIdentityNormalization.Reason("secret\nvalue")).Code);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("nested//file")]
    [InlineData("/leading")]
    [InlineData("trailing/")]
    [InlineData("folder\\file")]
    public void ObjectKeysRejectProviderPathTraversalShapes(string key)
    {
        Assert.Throws<ArgumentException>(() => ObjectStoreKey.Create(key));
    }

    [Fact]
    public void ImportSourcePolicyRequiresAProviderScopeAndPositiveAllowance()
    {
        var policy = CustomerImportSourcePolicy.Create("namespace/bucket", 100);
        Assert.Equal("namespace/bucket", policy.ProviderScope);
        Assert.Throws<CustomerValidationException>(() => CustomerImportSourcePolicy.Create("", 100));
        Assert.Throws<CustomerValidationException>(() => CustomerImportSourcePolicy.Create("bucket", 0));
    }

    [Fact]
    public async Task SourceRetirementKeepsAmbiguousDeletesRecoverable()
    {
        var tenantId = Guid.NewGuid();
        var lease = new CustomerImportSourceRetirementLease("imports/source.csv", "bucket", 42, new string('a', 64));
        var source = new RetirementStore(lease);
        var objectStore = new RetirementObjectStore(
            new(ObjectStoreDeleteOutcome.OutcomeUnknown), new(ObjectStoreDeleteOutcome.NotFound));
        var reconciler = new ReconcileCustomerImportSource(source, objectStore);

        Assert.False(await reconciler.RetireOneExpiredAsync(tenantId, DateTimeOffset.UtcNow, CancellationToken.None));
        Assert.Equal(("imports/source.csv", false, "OutcomeUnknown"), Assert.Single(source.Completions));
        Assert.True(await reconciler.RetireOneExpiredAsync(tenantId, DateTimeOffset.UtcNow, CancellationToken.None));
        Assert.Equal(("imports/source.csv", true, null), source.Completions[1]);
        Assert.Equal(2, objectStore.DeleteCalls);
    }

    [Theory]
    [InlineData("name\n\"Alice\"unexpected\n")]
    [InlineData("name\nAli\"ce\n")]
    [InlineData("name\n\"unfinished\n")]
    public async Task CsvRejectsMalformedQuoting(string text)
    {
        Assert.Equal("import_csv_invalid", (await Assert.ThrowsAsync<CustomerValidationException>(() =>
            CustomerImportCsv.PlanAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)),
                Guid.NewGuid(), CancellationToken.None))).Code);
    }

    [Fact]
    public async Task CsvRejectsInvalidUtf8AndByteAndRowLimits()
    {
        Assert.Equal("import_encoding_invalid", (await Assert.ThrowsAsync<CustomerValidationException>(() =>
            CustomerImportCsv.PlanAsync(new MemoryStream([0xFF]), Guid.NewGuid(), CancellationToken.None))).Code);
        Assert.Equal("import_too_large", (await Assert.ThrowsAsync<CustomerValidationException>(() =>
            CustomerImportCsv.PlanAsync(new MemoryStream(new byte[CustomerImportCsv.MaxBytes + 1]), Guid.NewGuid(), CancellationToken.None))).Code);
        var tooMany = "name\n" + string.Concat(Enumerable.Repeat("Person\n", CustomerImportCsv.MaxRows + 1));
        Assert.Equal("import_row_limit_exceeded", (await Assert.ThrowsAsync<CustomerValidationException>(() =>
            CustomerImportCsv.PlanAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(tooMany)), Guid.NewGuid(), CancellationToken.None))).Code);
        var template = await CustomerImportCsv.PlanAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(CustomerImportCsv.Template)),
            Guid.NewGuid(), CancellationToken.None);
        Assert.Empty(template.Rows);
    }
    [Fact]
    public void SignalsNormalizePhoneEmailExternalIdAndRelationships()
    {
        var organization = Guid.NewGuid();
        var program = Guid.NewGuid();
        var signals = CustomerDuplicateSignals.Create(
            "  Cafe\u0301  ", " Person@Example.Test ", "+1 (555) 0100", " ERP- 7 ", organization, program);

        Assert.Equal("CAFÉ", signals.NormalizedName);
        Assert.Equal("PERSON@EXAMPLE.TEST", signals.NormalizedEmail);
        Assert.Equal("15550100", signals.NormalizedPhone);
        Assert.Equal("ERP- 7", signals.NormalizedExternalRegistrationId);
        Assert.Equal(organization, signals.OrganizationId);
        Assert.Equal(program, signals.ProgramId);
    }

    [Fact]
    public void FuzzyNameIsDiscoveryEvidenceAndNeverAConsolidationOutcome()
    {
        var target = CustomerDuplicateSignals.Create("Alice Smith");
        var candidate = new CustomerIndividualSnapshot(Guid.NewGuid(), Guid.NewGuid(), "Alic Smith", null, null,
            CustomerIndividualAvailability.Active, 1, Guid.NewGuid(), DateTimeOffset.UtcNow, null, null);
        var matches = CustomerDuplicateMatcher.Discover(candidate.TenantId, target,
            [new(candidate, CustomerDuplicateSignals.Create("Alic Smith"))], null, 10, true);

        var evidence = Assert.Single(matches).Evidence;
        Assert.Contains(evidence, item => item.Kind == CustomerDuplicateEvidenceKind.FuzzyNameDiscovery);
        Assert.DoesNotContain(evidence, item => item.Kind == CustomerDuplicateEvidenceKind.NormalizedEmail);
        Assert.Equal("ConsolidateInto", CustomerDuplicateOutcome.ConsolidateInto.ToString());
    }

    [Fact]
    public void ConsolidatedSourceIsNeverOfferedAsALiveDiscoveryCandidate()
    {
        var tenant = Guid.NewGuid();
        var target = CustomerDuplicateSignals.Create("Alice Smith", "shared@example.test");
        var signals = CustomerDuplicateSignals.Create("Alice Smith", "shared@example.test");
        var live = new CustomerIndividualSnapshot(Guid.NewGuid(), tenant, "Alice Smith", "shared@example.test", null,
            CustomerIndividualAvailability.Active, 1, Guid.NewGuid(), DateTimeOffset.UtcNow, null, null);
        var consolidated = new CustomerIndividualSnapshot(Guid.NewGuid(), tenant, "Alice Smith", "shared@example.test", null,
            CustomerIndividualAvailability.Active, 3, Guid.NewGuid(), DateTimeOffset.UtcNow, null, null,
            RedirectTargetIndividualId: live.IndividualId);
        var otherTenant = live with { TenantId = Guid.NewGuid() };

        var matches = CustomerDuplicateMatcher.Discover(tenant, target,
            [new(live, signals), new(consolidated, signals), new(otherTenant, signals)], null, 10, false);

        Assert.Equal(live.IndividualId, Assert.Single(matches).Customer.IndividualId);
    }

    [Fact]
    public async Task ResolveAuthorityRejectsConsolidationOutcome()
    {
        var store = new NoCallResolutionStore();
        var context = await CreateTenantContextAsync();
        var exception = await Assert.ThrowsAsync<CustomerValidationException>(() =>
            new ResolveCustomerDuplicate(store).ExecuteAsync(context,
                new(Guid.NewGuid(), Guid.NewGuid(), 1, 1, CustomerDuplicateOutcome.ConsolidateInto),
                "resolve-1", CancellationToken.None));

        Assert.Equal("consolidation_requires_separate_authority", exception.Code);
        Assert.False(store.Called);
    }

    [Fact]
    public async Task CsvPlanRetainsManifestAndRowHashesWithoutMutatingCustomers()
    {
        const string csv = "name,external_id,email,phone,notes\n" +
                           "Alice, ERP-1 , alice@example.test ,+1 (555) 0100,hello\n" +
                           "Broken\n";
        var plan = await CustomerImportCsv.PlanAsync(
            new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv)), Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(CustomerImportCsv.ContractVersion, plan.ContractVersion);
        Assert.Equal(2, plan.Rows.Count);
        Assert.Equal(CustomerImportRowStatus.Pending, plan.Rows[0].Status);
        Assert.Equal("ERP-1", plan.Rows[0].Intent!.ExternalRegistrationId);
        Assert.Equal(CustomerImportRowStatus.Rejected, plan.Rows[1].Status);
        Assert.Equal("row_shape_invalid", plan.Rows[1].ErrorCode);
        Assert.Equal(64, plan.ManifestHash.Length);
        Assert.All(plan.Rows, row => Assert.Equal(64, row.SourceRowHash.Length));
    }

    [Theory]
    [InlineData("name,unknown\nAlice,1\n", "import_header_invalid")]
    [InlineData("email\na@example.test\n", "import_name_header_missing")]
    public async Task CsvPlanRejectsUnsupportedOrMissingHeaders(string csv, string code)
    {
        var exception = await Assert.ThrowsAsync<CustomerValidationException>(() => CustomerImportCsv.PlanAsync(
            new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv)), Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(code, exception.Code);
    }

    private static async Task<TenantContext> CreateTenantContextAsync() =>
        (await new ResolveTenantContext(new ActiveMembershipDirectory()).ExecuteAsync(
            Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None))!;

    private sealed class ActiveMembershipDirectory : ITenantMembershipDirectory
    {
        public Task<IReadOnlyList<TenantMembership>> ListActiveAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<bool> IsActiveAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class NoCallResolutionStore : ICustomerDuplicateResolutionStore
    {
        public bool Called { get; private set; }

        public Task<ResolveCustomerDuplicateResult> ResolveDuplicateAsync(TenantContext context,
            ResolveCustomerDuplicateRequest request, string idempotencyKey, CancellationToken cancellationToken)
        {
            Called = true;
            return Task.FromResult(new ResolveCustomerDuplicateResult(ResolveCustomerDuplicateStatus.Resolved, null));
        }
    }

    private sealed class RetirementStore(CustomerImportSourceRetirementLease lease) : ICustomerImportSourceMaintenanceStore
    {
        public List<(string ObjectKey, bool Deleted, string? FailureCode)> Completions { get; } = [];

        public Task<CustomerImportSourceRetirementLease?> ClaimExpiredSourceRetirementAsync(
            Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult<CustomerImportSourceRetirementLease?>(lease);

        public Task CompleteSourceRetirementAsync(Guid tenantId, CustomerImportSourceRetirementLease completion,
            bool deleted, string? failureCode, CancellationToken cancellationToken)
        {
            Completions.Add((completion.ObjectKey, deleted, failureCode));
            return Task.CompletedTask;
        }
    }

    private sealed class RetirementObjectStore(params ObjectStoreDeleteResult[] results) : IObjectStore
    {
        private int _index;
        public int DeleteCalls => _index;

        public Task<ObjectStorePutResult> PutAsync(ObjectStorePutRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ObjectStoreReadResult> OpenReadAsync(ObjectStoreReadRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ObjectStoreMetadataResult> GetMetadataAsync(ObjectStoreKey key, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ObjectStoreDeleteResult> DeleteAsync(ObjectStoreKey key, CancellationToken cancellationToken) =>
            Task.FromResult(results[Math.Min(_index++, results.Length - 1)]);
    }
}
