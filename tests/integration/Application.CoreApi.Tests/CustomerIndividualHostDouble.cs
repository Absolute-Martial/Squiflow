using Application.Customers;
using Application.Tenancy;

namespace Application.CoreApi.Tests;

internal sealed partial class TestCustomerStore
{
    private readonly Dictionary<(Guid Tenant, Guid Id), CustomerIndividualSnapshot> _individuals = [];

    public Task<CreateCustomerIndividualResult> CreateIndividualAsync(TenantContext context,
        CustomerIndividualIntent intent, string idempotencyKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Count(context.TenantId);
            var key = (context.TenantId, context.AccountId, "individual", idempotencyKey);
            if (_receipts.TryGetValue(key, out var receipt))
                return Task.FromResult(receipt.Fingerprint == intent.Fingerprint
                    ? new CreateCustomerIndividualResult(CreateCustomerIndividualStatus.Replayed, (CustomerIndividualSnapshot)receipt.Value)
                    : new CreateCustomerIndividualResult(CreateCustomerIndividualStatus.IdempotencyKeyConflict, null));
            var value = new CustomerIndividualSnapshot(Guid.NewGuid(), context.TenantId, intent.DisplayName,
                intent.Email, intent.Phone, CustomerIndividualAvailability.Active, 1, context.AccountId, DateTimeOffset.UtcNow, null, null);
            _individuals[(context.TenantId, value.IndividualId)] = value;
            _receipts.Add(key, (intent.Fingerprint, value));
            return Task.FromResult(new CreateCustomerIndividualResult(CreateCustomerIndividualStatus.Created, value));
        }
    }

    public Task<CustomerIndividualSnapshot?> FindIndividualAsync(TenantContext context, Guid individualId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Count(context.TenantId);
            return Task.FromResult(_individuals.GetValueOrDefault((context.TenantId, individualId)));
        }
    }

    public Task<EditCustomerIndividualContactResult> EditIndividualContactAsync(
        TenantContext context, CustomerIndividualContactIntent intent, string idempotencyKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Count(context.TenantId);
            var key = (context.TenantId, context.AccountId, "individual-contact", idempotencyKey);
            if (_receipts.TryGetValue(key, out var receipt))
                return Task.FromResult(receipt.Fingerprint == intent.Fingerprint
                    ? new EditCustomerIndividualContactResult(EditCustomerIndividualContactStatus.Replayed, (CustomerIndividualSnapshot)receipt.Value)
                    : new EditCustomerIndividualContactResult(EditCustomerIndividualContactStatus.IdempotencyKeyConflict, null));
            if (!_individuals.TryGetValue((context.TenantId, intent.IndividualId), out var current))
                return Task.FromResult(new EditCustomerIndividualContactResult(EditCustomerIndividualContactStatus.NotFound, null));
            if (current.Revision != intent.ExpectedRevision)
                return Task.FromResult(new EditCustomerIndividualContactResult(EditCustomerIndividualContactStatus.RevisionConflict, current));
            if (current.DisplayName == intent.DisplayName && current.Email == intent.Email && current.Phone == intent.Phone)
                return Task.FromResult(new EditCustomerIndividualContactResult(EditCustomerIndividualContactStatus.NoChange, current));
            var changed = current with
            {
                DisplayName = intent.DisplayName,
                Email = intent.Email,
                Phone = intent.Phone,
                Revision = current.Revision + 1,
                ContactChangedAt = DateTimeOffset.UtcNow,
                ContactChangedByAccountId = context.AccountId
            };
            _individuals[(context.TenantId, intent.IndividualId)] = changed;
            _receipts.Add(key, (intent.Fingerprint, changed));
            return Task.FromResult(new EditCustomerIndividualContactResult(EditCustomerIndividualContactStatus.Changed, changed));
        }
    }

    public Task<ChangeCustomerIndividualAvailabilityResult> ChangeIndividualAvailabilityAsync(TenantContext context,
        CustomerIndividualAvailabilityIntent intent, string idempotencyKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Count(context.TenantId);
            var key = (context.TenantId, context.AccountId, "individual-availability", idempotencyKey);
            if (_receipts.TryGetValue(key, out var receipt))
                return Task.FromResult(receipt.Fingerprint == intent.Fingerprint
                    ? new ChangeCustomerIndividualAvailabilityResult(ChangeCustomerIndividualAvailabilityStatus.Replayed, (CustomerIndividualSnapshot)receipt.Value)
                    : new ChangeCustomerIndividualAvailabilityResult(ChangeCustomerIndividualAvailabilityStatus.IdempotencyKeyConflict, null));
            if (!_individuals.TryGetValue((context.TenantId, intent.IndividualId), out var current))
                return Task.FromResult(new ChangeCustomerIndividualAvailabilityResult(ChangeCustomerIndividualAvailabilityStatus.NotFound, null));
            if (current.Revision != intent.ExpectedRevision)
                return Task.FromResult(new ChangeCustomerIndividualAvailabilityResult(ChangeCustomerIndividualAvailabilityStatus.RevisionConflict, null));
            if (current.Availability == intent.Availability)
                return Task.FromResult(new ChangeCustomerIndividualAvailabilityResult(ChangeCustomerIndividualAvailabilityStatus.AlreadyInState, null));
            var changed = current with
            {
                Availability = intent.Availability,
                Revision = current.Revision + 1,
                AvailabilityChangedAt = DateTimeOffset.UtcNow,
                AvailabilityChangedByAccountId = context.AccountId
            };
            _individuals[(context.TenantId, intent.IndividualId)] = changed;
            _receipts.Add(key, (intent.Fingerprint, changed));
            return Task.FromResult(new ChangeCustomerIndividualAvailabilityResult(ChangeCustomerIndividualAvailabilityStatus.Changed, changed));
        }
    }
}
