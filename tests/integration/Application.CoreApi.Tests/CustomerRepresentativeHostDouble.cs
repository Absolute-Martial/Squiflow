using Application.Customers;
using Application.Tenancy;

namespace Application.CoreApi.Tests;

internal sealed partial class TestCustomerStore
{
    private readonly Dictionary<(Guid Tenant, Guid Id), CustomerRepresentativeSnapshot> _representatives = [];

    public Task<LinkCustomerRepresentativeResult> LinkRepresentativeAsync(
        TenantContext context, CustomerRepresentativeLinkIntent intent, string idempotencyKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Count(context.TenantId);
            var key = (context.TenantId, context.AccountId, "representative-link", idempotencyKey);
            if (_receipts.TryGetValue(key, out var receipt))
                return Task.FromResult(receipt.Fingerprint == intent.Fingerprint
                    ? new LinkCustomerRepresentativeResult(LinkCustomerRepresentativeStatus.Replayed, (CustomerRepresentativeSnapshot)receipt.Value)
                    : new LinkCustomerRepresentativeResult(LinkCustomerRepresentativeStatus.IdempotencyKeyConflict, null));
            if (!_organizations.ContainsKey((context.TenantId, intent.OrganizationId)))
                return Task.FromResult(new LinkCustomerRepresentativeResult(LinkCustomerRepresentativeStatus.TargetNotFound, null));
            if (intent.ProgramId is Guid programId &&
                (!_programs.TryGetValue((context.TenantId, programId), out var program) || program.OrganizationId != intent.OrganizationId))
                return Task.FromResult(new LinkCustomerRepresentativeResult(LinkCustomerRepresentativeStatus.TargetNotFound, null));
            if (!_individuals.TryGetValue((context.TenantId, intent.IndividualId), out var individual))
                return Task.FromResult(new LinkCustomerRepresentativeResult(LinkCustomerRepresentativeStatus.IndividualNotFound, null));
            if (individual.Availability != CustomerIndividualAvailability.Active)
                return Task.FromResult(new LinkCustomerRepresentativeResult(LinkCustomerRepresentativeStatus.IndividualInactive, null));

            var existing = _representatives.Values.SingleOrDefault(value =>
                value.TenantId == context.TenantId &&
                value.OrganizationId == intent.OrganizationId &&
                value.ProgramId == intent.ProgramId &&
                value.IndividualId == intent.IndividualId &&
                value.Availability == CustomerRepresentativeAvailability.Active);
            if (existing is not null)
                return Task.FromResult(new LinkCustomerRepresentativeResult(LinkCustomerRepresentativeStatus.AlreadyLinked, existing));

            var created = new CustomerRepresentativeSnapshot(
                Guid.NewGuid(), context.TenantId, intent.OrganizationId, intent.ProgramId, intent.IndividualId,
                CustomerRepresentativeAvailability.Active, 1, context.AccountId, DateTimeOffset.UtcNow, null, null);
            _representatives[(context.TenantId, created.RepresentativeId)] = created;
            _receipts.Add(key, (intent.Fingerprint, created));
            return Task.FromResult(new LinkCustomerRepresentativeResult(LinkCustomerRepresentativeStatus.Created, created));
        }
    }

    public Task<CustomerRepresentativeSnapshot?> FindRepresentativeAsync(
        TenantContext context, Guid representativeId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Count(context.TenantId);
            return Task.FromResult(_representatives.GetValueOrDefault((context.TenantId, representativeId)));
        }
    }

    public Task<UnlinkCustomerRepresentativeResult> UnlinkRepresentativeAsync(
        TenantContext context, CustomerRepresentativeUnlinkIntent intent, string idempotencyKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Count(context.TenantId);
            var key = (context.TenantId, context.AccountId, "representative-unlink", idempotencyKey);
            if (_receipts.TryGetValue(key, out var receipt))
                return Task.FromResult(receipt.Fingerprint == intent.Fingerprint
                    ? new UnlinkCustomerRepresentativeResult(UnlinkCustomerRepresentativeStatus.Replayed, (CustomerRepresentativeSnapshot)receipt.Value)
                    : new UnlinkCustomerRepresentativeResult(UnlinkCustomerRepresentativeStatus.IdempotencyKeyConflict, null));
            if (!_representatives.TryGetValue((context.TenantId, intent.RepresentativeId), out var current) ||
                current.OrganizationId != intent.OrganizationId)
                return Task.FromResult(new UnlinkCustomerRepresentativeResult(UnlinkCustomerRepresentativeStatus.NotFound, null));
            if (current.Revision != intent.ExpectedRevision)
                return Task.FromResult(new UnlinkCustomerRepresentativeResult(UnlinkCustomerRepresentativeStatus.RevisionConflict, current));
            if (current.Availability == CustomerRepresentativeAvailability.Inactive)
                return Task.FromResult(new UnlinkCustomerRepresentativeResult(UnlinkCustomerRepresentativeStatus.AlreadyInactive, current));
            var changed = current with
            {
                Availability = CustomerRepresentativeAvailability.Inactive,
                Revision = current.Revision + 1,
                ChangedByAccountId = context.AccountId,
                ChangedAt = DateTimeOffset.UtcNow
            };
            _representatives[(context.TenantId, intent.RepresentativeId)] = changed;
            _receipts.Add(key, (intent.Fingerprint, changed));
            return Task.FromResult(new UnlinkCustomerRepresentativeResult(UnlinkCustomerRepresentativeStatus.Changed, changed));
        }
    }
}
