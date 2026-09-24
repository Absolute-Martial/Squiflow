using Application.CoreApi.Authorization;
using Application.Customers;
using Application.Tenancy;

namespace Application.CoreApi.Tests;

internal sealed class TestTenantCustomerAuthorization : ITenantCustomerAuthorization
{
    private readonly Dictionary<(Guid, Guid, string), (bool Allowed, bool Unavailable)> _decisions = [];
    private readonly Dictionary<(Guid, Guid, string), int> _checks = [];
    private readonly object _gate = new();

    public void Set(Guid account, Guid tenant, string operation, bool allowed)
    {
        lock (_gate) _decisions[(account, tenant, operation)] = (allowed, false);
    }

    public void SetUnavailable(Guid account, Guid tenant, string operation)
    {
        lock (_gate) _decisions[(account, tenant, operation)] = (false, true);
    }

    public int CheckCount(Guid account, Guid tenant, string operation)
    {
        lock (_gate) return _checks.GetValueOrDefault((account, tenant, operation));
    }

    public Task<bool> CanCreateOrganizationAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Check(accountId, tenantId, "createOrganization", ct);
    public Task<bool> CanViewOrganizationsAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Check(accountId, tenantId, "viewOrganizations", ct);
    public Task<bool> CanCreateProgramAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Check(accountId, tenantId, "createProgram", ct);
    public Task<bool> CanViewProgramsAsync(Guid accountId, Guid tenantId, CancellationToken ct) => Check(accountId, tenantId, "viewPrograms", ct);

    private Task<bool> Check(Guid account, Guid tenant, string operation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var key = (account, tenant, operation);
            _checks[key] = _checks.GetValueOrDefault(key) + 1;
            var value = _decisions.GetValueOrDefault(key);
            if (value.Unavailable)
                throw new AuthorizationProviderUnavailableException("Synthetic outage.", new HttpRequestException("Synthetic outage."));
            return Task.FromResult(value.Allowed);
        }
    }
}

internal sealed class TestCustomerStore : ICustomerStore
{
    private readonly Dictionary<(Guid Tenant, Guid Id), CustomerOrganizationSnapshot> _organizations = [];
    private readonly Dictionary<(Guid Tenant, Guid Id), CustomerProgramSnapshot> _programs = [];
    private readonly Dictionary<(Guid Tenant, Guid Account, string Kind, string Key), (string Fingerprint, object Value)> _receipts = [];
    private readonly Dictionary<Guid, int> _calls = [];
    private readonly object _gate = new();

    public int CallCount(Guid tenant)
    {
        lock (_gate) return _calls.GetValueOrDefault(tenant);
    }

    public Task<CreateCustomerOrganizationResult> CreateOrganizationAsync(TenantContext context, CustomerOrganizationIntent intent, string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Count(context.TenantId);
            var receiptKey = (context.TenantId, context.AccountId, "organization", key);
            if (_receipts.TryGetValue(receiptKey, out var receipt))
                return Task.FromResult(receipt.Fingerprint == intent.Fingerprint
                    ? new CreateCustomerOrganizationResult(CreateCustomerOrganizationStatus.Replayed, (CustomerOrganizationSnapshot)receipt.Value)
                    : new CreateCustomerOrganizationResult(CreateCustomerOrganizationStatus.IdempotencyKeyConflict, null));
            var value = new CustomerOrganizationSnapshot(Guid.NewGuid(), context.TenantId, intent.DisplayName, DateTimeOffset.UtcNow);
            _organizations[(context.TenantId, value.OrganizationId)] = value;
            _receipts[receiptKey] = (intent.Fingerprint, value);
            return Task.FromResult(new CreateCustomerOrganizationResult(CreateCustomerOrganizationStatus.Created, value));
        }
    }

    public Task<CreateCustomerProgramResult> CreateProgramAsync(TenantContext context, CustomerProgramIntent intent, string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Count(context.TenantId);
            var receiptKey = (context.TenantId, context.AccountId, "program", key);
            if (_receipts.TryGetValue(receiptKey, out var receipt))
                return Task.FromResult(receipt.Fingerprint == intent.Fingerprint
                    ? new CreateCustomerProgramResult(CreateCustomerProgramStatus.Replayed, (CustomerProgramSnapshot)receipt.Value)
                    : new CreateCustomerProgramResult(CreateCustomerProgramStatus.IdempotencyKeyConflict, null));
            if (!_organizations.ContainsKey((context.TenantId, intent.OrganizationId)))
                return Task.FromResult(new CreateCustomerProgramResult(CreateCustomerProgramStatus.ParentNotFound, null));
            var value = new CustomerProgramSnapshot(Guid.NewGuid(), context.TenantId, intent.OrganizationId, intent.DisplayName, DateTimeOffset.UtcNow);
            _programs[(context.TenantId, value.ProgramId)] = value;
            _receipts[receiptKey] = (intent.Fingerprint, value);
            return Task.FromResult(new CreateCustomerProgramResult(CreateCustomerProgramStatus.Created, value));
        }
    }

    public Task<CustomerOrganizationSnapshot?> FindOrganizationAsync(TenantContext context, Guid id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Count(context.TenantId);
            return Task.FromResult(_organizations.GetValueOrDefault((context.TenantId, id)));
        }
    }

    public Task<CustomerProgramSnapshot?> FindProgramAsync(TenantContext context, Guid id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Count(context.TenantId);
            return Task.FromResult(_programs.GetValueOrDefault((context.TenantId, id)));
        }
    }

    public Task<CustomerOrganizationPage> ListOrganizationsAsync(TenantContext context, ListCustomerOrganizationsRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Count(context.TenantId);
            var rows = _organizations.Values.Where(x => x.TenantId == context.TenantId)
                .OrderBy(x => x.CreatedAt).ThenBy(x => x.OrganizationId)
                .Where(x => request.After is null || x.CreatedAt > request.After.CreatedAt ||
                    x.CreatedAt == request.After.CreatedAt && x.OrganizationId.CompareTo(request.After.OrganizationId) > 0)
                .Take(request.Limit + 1).ToArray();
            var items = rows.Take(request.Limit).ToArray();
            return Task.FromResult(new CustomerOrganizationPage(items,
                rows.Length > request.Limit ? new CustomerOrganizationPageCursor(items[^1].CreatedAt, items[^1].OrganizationId) : null));
        }
    }

    public Task<CustomerProgramPage> ListProgramsAsync(TenantContext context, ListCustomerProgramsRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Count(context.TenantId);
            var rows = _programs.Values.Where(x => x.TenantId == context.TenantId && x.OrganizationId == request.OrganizationId)
                .OrderBy(x => x.CreatedAt).ThenBy(x => x.ProgramId)
                .Where(x => request.After is null || x.CreatedAt > request.After.CreatedAt ||
                    x.CreatedAt == request.After.CreatedAt && x.ProgramId.CompareTo(request.After.ProgramId) > 0)
                .Take(request.Limit + 1).ToArray();
            var items = rows.Take(request.Limit).ToArray();
            return Task.FromResult(new CustomerProgramPage(items,
                rows.Length > request.Limit ? new CustomerProgramPageCursor(items[^1].CreatedAt, items[^1].ProgramId) : null));
        }
    }

    public Task<CustomerOrderContext?> ResolveOrderContextAsync(TenantContext context, Guid organizationId, Guid? programId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Count(context.TenantId);
            return Task.FromResult<CustomerOrderContext?>(_organizations.ContainsKey((context.TenantId, organizationId)) &&
                (!programId.HasValue || _programs.TryGetValue((context.TenantId, programId.Value), out var program) && program.OrganizationId == organizationId)
                ? new CustomerOrderContext(organizationId, programId) : null);
        }
    }

    private void Count(Guid tenant) => _calls[tenant] = _calls.GetValueOrDefault(tenant) + 1;
}
