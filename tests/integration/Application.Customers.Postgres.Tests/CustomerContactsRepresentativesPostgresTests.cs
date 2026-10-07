using Application.Customers;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Application.Customers.Postgres.Tests;

public sealed partial class CustomerPostgresTests
{
    [Fact]
    public async Task ContactEditIsRevisionCheckedReplaySafeAndTenantIsolated()
    {
        await MigrateAsync();
        var (tenantA, accountA) = await SeedAsync();
        var (tenantB, accountB) = await SeedAsync();
        var contextA = await ResolveContextAsync(tenantA, accountA);
        var contextB = await ResolveContextAsync(tenantB, accountB);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync());
        var store = new PostgresCustomerStore(source);
        var created = (await new CreateCustomerIndividual(store).ExecuteAsync(
            contextA, new("Person", "person@example.test", "+977 12345"),
            "create-person", CancellationToken.None)).Individual!;
        var edit = new EditCustomerIndividualContact(store);

        var wrongTenant = await edit.ExecuteAsync(contextB,
            new(created.IndividualId, 1, "Hidden", null, null),
            "wrong-tenant", CancellationToken.None);
        Assert.Equal(EditCustomerIndividualContactStatus.NotFound, wrongTenant.Status);

        var request = new EditCustomerIndividualContactRequest(
            created.IndividualId, 1, "  Cafe\u0301 Person  ", " changed@example.test ", null);
        var attempts = await Task.WhenAll(Enumerable.Range(0, 4).Select(index =>
            edit.ExecuteAsync(contextA, request, $"edit-contact-{index}", CancellationToken.None)));
        Assert.Single(attempts, result => result.Status == EditCustomerIndividualContactStatus.Changed);
        Assert.Equal(3, attempts.Count(result =>
            result.Status == EditCustomerIndividualContactStatus.RevisionConflict));
        var winner = attempts.Select((result, index) => (result, index)).Single(item =>
            item.result.Status == EditCustomerIndividualContactStatus.Changed);
        var changed = winner.result;
        Assert.Equal(2, changed.Individual!.Revision);
        Assert.Equal("Café Person", changed.Individual.DisplayName);
        Assert.Equal("changed@example.test", changed.Individual.Email);
        Assert.Null(changed.Individual.Phone);
        Assert.Equal(accountA, changed.Individual.ContactChangedByAccountId);
        Assert.NotNull(changed.Individual.ContactChangedAt);

        var replay = await edit.ExecuteAsync(contextA, request, $"edit-contact-{winner.index}", CancellationToken.None);
        Assert.Equal(EditCustomerIndividualContactStatus.Replayed, replay.Status);
        Assert.Equal(changed.Individual, replay.Individual);
        var keyConflict = await edit.ExecuteAsync(contextA, request with { Email = "other@example.test" },
            $"edit-contact-{winner.index}", CancellationToken.None);
        Assert.Equal(EditCustomerIndividualContactStatus.IdempotencyKeyConflict, keyConflict.Status);
        var stale = await edit.ExecuteAsync(contextA, request with { DisplayName = "Stale name" },
            "stale-contact", CancellationToken.None);
        Assert.Equal(EditCustomerIndividualContactStatus.RevisionConflict, stale.Status);
        Assert.Equal(changed.Individual, stale.Individual);
        var noChange = await edit.ExecuteAsync(contextA, new(
            created.IndividualId, 2, changed.Individual.DisplayName, changed.Individual.Email, changed.Individual.Phone),
            "no-change", CancellationToken.None);
        Assert.Equal(EditCustomerIndividualContactStatus.NoChange, noChange.Status);
        Assert.Null(await store.FindIndividualAsync(contextB, created.IndividualId, CancellationToken.None));
        Assert.Equal(2L, await CountAsync("customers.individual_command_receipts"));
    }

    [Fact]
    public async Task RepresentativeRelationshipValidatesProgramAndIndividualAndUnlinksWithReplay()
    {
        await MigrateAsync();
        var (tenant, account) = await SeedAsync();
        var (otherTenant, otherAccount) = await SeedAsync();
        var context = await ResolveContextAsync(tenant, account);
        var otherContext = await ResolveContextAsync(otherTenant, otherAccount);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync());
        var store = new PostgresCustomerStore(source);
        var organizations = new CreateCustomerOrganization(store);
        var programs = new CreateCustomerProgram(store);
        var individuals = new CreateCustomerIndividual(store);
        var links = new LinkCustomerRepresentative(store);
        var unlinks = new UnlinkCustomerRepresentative(store);

        var organizationA = (await organizations.ExecuteAsync(context, new("Organization A"), "org-a",
            CancellationToken.None)).Organization!;
        var organizationB = (await organizations.ExecuteAsync(context, new("Organization B"), "org-b",
            CancellationToken.None)).Organization!;
        var programA = (await programs.ExecuteAsync(context, new(organizationA.OrganizationId, "Program A"), "program-a",
            CancellationToken.None)).Program!;
        var individual = (await individuals.ExecuteAsync(context, new("Representative", "rep@example.test", null),
            "rep-person", CancellationToken.None)).Individual!;

        var wrongParent = await links.ExecuteAsync(context,
            new(organizationB.OrganizationId, programA.ProgramId, individual.IndividualId),
            "wrong-parent", CancellationToken.None);
        Assert.Equal(LinkCustomerRepresentativeStatus.TargetNotFound, wrongParent.Status);

        var request = new LinkCustomerRepresentativeRequest(
            organizationA.OrganizationId, programA.ProgramId, individual.IndividualId);
        var linkAttempts = await Task.WhenAll(Enumerable.Range(0, 4).Select(index =>
            links.ExecuteAsync(context, request, $"link-{index}", CancellationToken.None)));
        Assert.Single(linkAttempts, result => result.Status == LinkCustomerRepresentativeStatus.Created);
        Assert.Equal(3, linkAttempts.Count(result => result.Status == LinkCustomerRepresentativeStatus.AlreadyLinked));
        var linkWinner = linkAttempts.Select((result, index) => (result, index)).Single(item =>
            item.result.Status == LinkCustomerRepresentativeStatus.Created);
        var linked = linkWinner.result;
        Assert.Equal(CustomerRepresentativeAvailability.Active, linked.Representative!.Availability);
        Assert.Equal(1, linked.Representative.Revision);
        Assert.Equal(account, linked.Representative.CreatedByAccountId);
        Assert.Equal(programA.ProgramId, linked.Representative.ProgramId);
        Assert.Null(await store.FindRepresentativeAsync(
            otherContext, linked.Representative.RepresentativeId, CancellationToken.None));

        var replay = await links.ExecuteAsync(context, request, $"link-{linkWinner.index}", CancellationToken.None);
        Assert.Equal(LinkCustomerRepresentativeStatus.Replayed, replay.Status);
        Assert.Equal(linked.Representative, replay.Representative);
        var duplicate = await links.ExecuteAsync(context, request, "duplicate-link", CancellationToken.None);
        Assert.Equal(LinkCustomerRepresentativeStatus.AlreadyLinked, duplicate.Status);

        var wrongOrganization = await unlinks.ExecuteAsync(context,
            new(organizationB.OrganizationId, linked.Representative.RepresentativeId, 1),
            "wrong-org", CancellationToken.None);
        Assert.Equal(UnlinkCustomerRepresentativeStatus.NotFound, wrongOrganization.Status);
        var unlinkRequest = new UnlinkCustomerRepresentativeRequest(
            organizationA.OrganizationId, linked.Representative.RepresentativeId, 1);
        var unlinkAttempts = await Task.WhenAll(Enumerable.Range(0, 4).Select(index =>
            unlinks.ExecuteAsync(context, unlinkRequest, $"unlink-{index}", CancellationToken.None)));
        Assert.Single(unlinkAttempts, result => result.Status == UnlinkCustomerRepresentativeStatus.Changed);
        Assert.Equal(3, unlinkAttempts.Count(result => result.Status == UnlinkCustomerRepresentativeStatus.RevisionConflict));
        var unlinkWinner = unlinkAttempts.Select((result, index) => (result, index)).Single(item =>
            item.result.Status == UnlinkCustomerRepresentativeStatus.Changed);
        var unlinked = unlinkWinner.result;
        Assert.Equal(CustomerRepresentativeAvailability.Inactive, unlinked.Representative!.Availability);
        Assert.Equal(2, unlinked.Representative.Revision);
        Assert.Equal(account, unlinked.Representative.ChangedByAccountId);
        Assert.NotNull(unlinked.Representative.ChangedAt);
        var unlinkReplay = await unlinks.ExecuteAsync(context,
            new(organizationA.OrganizationId, linked.Representative.RepresentativeId, 1),
            $"unlink-{unlinkWinner.index}", CancellationToken.None);
        Assert.Equal(UnlinkCustomerRepresentativeStatus.Replayed, unlinkReplay.Status);
        Assert.Equal(unlinked.Representative, unlinkReplay.Representative);
        var stale = await unlinks.ExecuteAsync(context,
            new(organizationA.OrganizationId, linked.Representative.RepresentativeId, 1),
            "stale-unlink", CancellationToken.None);
        Assert.Equal(UnlinkCustomerRepresentativeStatus.RevisionConflict, stale.Status);

        var inactive = (await individuals.ExecuteAsync(context, new("Inactive person", null, null),
            "inactive-person", CancellationToken.None)).Individual!;
        var inactiveResult = await new ChangeCustomerIndividualAvailability(store).ExecuteAsync(context,
            new(inactive.IndividualId, 1, CustomerIndividualAvailability.Inactive),
            "inactive", CancellationToken.None);
        Assert.Equal(ChangeCustomerIndividualAvailabilityStatus.Changed, inactiveResult.Status);
        var inactiveLink = await links.ExecuteAsync(context,
            new(organizationA.OrganizationId, null, inactive.IndividualId),
            "inactive-link", CancellationToken.None);
        Assert.Equal(LinkCustomerRepresentativeStatus.IndividualInactive, inactiveLink.Status);
        Assert.Equal(2L, await CountAsync("customers.representative_command_receipts"));
    }

    [Fact]
    public async Task RepresentativeLinkWaitsForConcurrentDeactivationAndReturnsIndividualInactive()
    {
        await MigrateAsync();
        var (tenant, account) = await SeedAsync();
        var context = await ResolveContextAsync(tenant, account);
        await using var source = NpgsqlDataSource.Create(await CreateRestrictedRoleAsync());
        var store = new PostgresCustomerStore(source);
        var organization = (await new CreateCustomerOrganization(store).ExecuteAsync(
            context, new("Race organization"), "race-org", CancellationToken.None)).Organization!;
        var individual = (await new CreateCustomerIndividual(store).ExecuteAsync(
            context, new("Race representative", null, null), "race-individual", CancellationToken.None)).Individual!;

        await using var deactivationConnection = new NpgsqlConnection(ConnectionString);
        await deactivationConnection.OpenAsync();
        await using var deactivation = await deactivationConnection.BeginTransactionAsync();
        await using (var update = deactivationConnection.CreateCommand())
        {
            update.Transaction = deactivation;
            update.CommandText = """
                UPDATE customers.individuals
                SET availability = 2,
                    revision = revision + 1,
                    availability_changed_by_account_id = @account_id,
                    availability_changed_at = now()
                WHERE tenant_id = @tenant_id AND id = @individual_id
                """;
            update.Parameters.AddWithValue("tenant_id", tenant);
            update.Parameters.AddWithValue("individual_id", individual.IndividualId);
            update.Parameters.AddWithValue("account_id", account);
            Assert.Equal(1, await update.ExecuteNonQueryAsync());
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var link = new LinkCustomerRepresentative(store).ExecuteAsync(
            context,
            new(organization.OrganizationId, null, individual.IndividualId),
            "race-link",
            timeout.Token);

        await WaitForRepresentativeInsertLockAsync(timeout.Token);
        await deactivation.CommitAsync(timeout.Token);

        var result = await link;
        Assert.Equal(LinkCustomerRepresentativeStatus.IndividualInactive, result.Status);
        Assert.Null(result.Representative);
        var current = await store.FindIndividualAsync(context, individual.IndividualId, timeout.Token);
        Assert.Equal(CustomerIndividualAvailability.Inactive, current!.Availability);
        Assert.Equal(0L, await CountAsync("customers.representatives"));
        Assert.Equal(0L, await CountAsync("customers.representative_command_receipts"));
    }

    [Fact]
    public async Task ContactEditLinkAndUnlinkFailDuringActualPostgresOutage()
    {
        await MigrateAsync();
        var (tenant, account) = await SeedAsync();
        var context = await ResolveContextAsync(tenant, account);
        var runtime = new NpgsqlConnectionStringBuilder(await CreateRestrictedRoleAsync())
        {
            Timeout = 2,
            CommandTimeout = 2,
        }.ConnectionString;
        await using var source = NpgsqlDataSource.Create(runtime);
        var store = new PostgresCustomerStore(source);
        var organization = (await new CreateCustomerOrganization(store).ExecuteAsync(
            context, new("Outage organization"), "outage-org", CancellationToken.None)).Organization!;
        var linkedIndividual = (await new CreateCustomerIndividual(store).ExecuteAsync(
            context, new("Linked representative", null, null), "outage-linked", CancellationToken.None)).Individual!;
        var unlinkedIndividual = (await new CreateCustomerIndividual(store).ExecuteAsync(
            context, new("Unlinked representative", null, null), "outage-unlinked", CancellationToken.None)).Individual!;
        var linked = (await new LinkCustomerRepresentative(store).ExecuteAsync(
            context,
            new(organization.OrganizationId, null, linkedIndividual.IndividualId),
            "outage-initial-link",
            CancellationToken.None)).Representative!;

        await _database.StopAsync();

        await Assert.ThrowsAnyAsync<NpgsqlException>(() =>
            new EditCustomerIndividualContact(store).ExecuteAsync(
                context,
                new(linkedIndividual.IndividualId, 1, "Changed while offline", null, null),
                "outage-edit",
                CancellationToken.None));
        await Assert.ThrowsAnyAsync<NpgsqlException>(() =>
            new LinkCustomerRepresentative(store).ExecuteAsync(
                context,
                new(organization.OrganizationId, null, unlinkedIndividual.IndividualId),
                "outage-link",
                CancellationToken.None));
        await Assert.ThrowsAnyAsync<NpgsqlException>(() =>
            new UnlinkCustomerRepresentative(store).ExecuteAsync(
                context,
                new(organization.OrganizationId, linked.RepresentativeId, 1),
                "outage-unlink",
                CancellationToken.None));
    }

    [Fact]
    public async Task ContactsAndRepresentativesMigrationIsCurrentAndForcesTenantRls()
    {
        await MigrateAsync();
        await using var db = CustomersPostgresMigrations.CreateContext(ConnectionString);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(1L, await CountAsync("pg_catalog.pg_class WHERE oid = 'customers.representatives'::regclass"));
        Assert.Equal(1L, await CountAsync("pg_catalog.pg_class WHERE oid = 'customers.representative_command_receipts'::regclass"));
        Assert.Equal(1L, await CountAsync("pg_catalog.pg_class WHERE oid = 'customers.representatives'::regclass AND relrowsecurity AND relforcerowsecurity"));
        Assert.Equal(1L, await CountAsync("pg_catalog.pg_class WHERE oid = 'customers.representative_command_receipts'::regclass AND relrowsecurity AND relforcerowsecurity"));
        Assert.Equal(1L, await CountAsync("information_schema.columns WHERE table_schema = 'customers' AND table_name = 'individuals' AND column_name = 'contact_changed_at'"));
    }

    private async Task WaitForRepresentativeInsertLockAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        while (true)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_stat_activity
                    WHERE datname = current_database()
                      AND wait_event_type = 'Lock'
                      AND query LIKE 'WITH active_individual AS (%')
                """;
            if (true.Equals(await command.ExecuteScalarAsync(cancellationToken)))
                return;
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        }
    }
}
