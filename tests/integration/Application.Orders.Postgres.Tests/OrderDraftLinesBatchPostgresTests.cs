using Application.Orders;
using Npgsql;
using Xunit;

namespace Application.Orders.Postgres.Tests;

// The order draft lines are inserted as one NpgsqlBatch holding one command per line.
// Every other Orders case persists a single-line draft, so a defect that only appears once
// a batch carries several commands - only the first command landing, parameters bleeding
// between batch commands, or positions collapsing - would otherwise pass unnoticed.
public sealed partial class OrderMigrationAndRlsTests
{
    [Fact]
    public async Task MultiLineDraftPersistsEveryBatchedLineInPositionOrderAndTotalsThemOnce()
    {
        await ApplyOrderSchemaAsync();
        var creator = Guid.CreateVersion7();
        var tenant = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(creator, tenant, Guid.CreateVersion7());
        await using var dataSource = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenant, creator);

        var created = await store.CreateAsync(context, MultiLineIntent("Four line batch"), "create", CancellationToken.None);
        Assert.Equal(CreateOrderDraftStatus.Created, created.Status);
        var order = created.Order!;

        // Proves every batched command executed, independent of the returned snapshot.
        Assert.Equal(4, await CountRowsAsync(
            "SELECT count(*) FROM orders.order_draft_lines WHERE tenant_id = @tenant_id", tenant));

        var read = await store.FindAsync(context, order.OrderId, CancellationToken.None);
        Assert.NotNull(read);
        Assert.Equal(4, read.Lines.Count);
        Assert.Equal([1, 2, 3, 4], read.Lines.Select(line => line.Position));
        Assert.Equal(
            ["Panel", "Frame", "Handle", "Cable"],
            read.Lines.Select(line => line.Description));
        Assert.Equal(
            [2m, 1m, 4m, 3m],
            read.Lines.Select(line => line.Quantity));
        Assert.Equal(
            ["EA", "EA", "BOX", "M"],
            read.Lines.Select(line => line.UnitCode));
        Assert.Equal(
            [12.5m, 40m, 3.25m, 1.1m],
            read.Lines.Select(line => line.UnitPrice));
        Assert.Equal(
            [25m, 40m, 13m, 3.3m],
            read.Lines.Select(line => line.LineTotal));
        Assert.Equal(81.3m, read.Total);
        Assert.Equal(read.Lines.Sum(line => line.LineTotal), read.Total);
    }

    [Fact]
    public async Task RevisingToADifferentLineCountReplacesEveryBatchedLine()
    {
        await ApplyOrderSchemaAsync();
        var creator = Guid.CreateVersion7();
        var tenant = Guid.CreateVersion7();
        await SeedAuthorityRowsAsync(creator, tenant, Guid.CreateVersion7());
        await using var dataSource = new NpgsqlDataSourceBuilder(await CreateRuntimeRoleAsync()).Build();
        var store = new PostgresOrderDraftStore(dataSource, new FixedTimeProvider());
        var context = await ResolveContextAsync(tenant, creator);

        var created = await store.CreateAsync(context, MultiLineIntent("Four line batch"), "create", CancellationToken.None);
        var order = created.Order!;

        // Shrinking the line count must leave exactly the revised lines, not a union or a residue.
        var request = new ReviseOrderDraftRequest(
            order.OrderId, 1, "Two line batch", "USD",
            [
                new OrderDraftLineInput("Panel", 2m, "EA", 12.5m),
                new OrderDraftLineInput("Cable", 3m, "M", 1.1m),
            ]);
        var intent = OrderDraftIntent.Create(new CreateOrderDraftRequest(
            request.Summary, request.CurrencyCode, request.Lines));
        var revised = await store.ReviseAsync(
            context, request, intent, "revise", new string('a', 64), CancellationToken.None);
        Assert.Equal(ReviseOrderDraftStatus.Revised, revised.Status);

        Assert.Equal(2, await CountRowsAsync(
            "SELECT count(*) FROM orders.order_draft_lines WHERE tenant_id = @tenant_id", tenant));

        var read = await store.FindAsync(context, order.OrderId, CancellationToken.None);
        Assert.NotNull(read);
        Assert.Equal(2, read.Lines.Count);
        Assert.Equal([1, 2], read.Lines.Select(line => line.Position));
        Assert.Equal(["Panel", "Cable"], read.Lines.Select(line => line.Description));
        Assert.Equal(28.3m, read.Total);
    }

    // Four lines with distinct positions, quantities, unit codes and prices so a batch that
    // reuses or reorders parameters cannot accidentally agree with the expected values.
    private static OrderDraftIntent MultiLineIntent(string summary) =>
        OrderDraftIntent.Create(new CreateOrderDraftRequest(
            summary,
            "USD",
            [
                new OrderDraftLineInput("Panel", 2m, "EA", 12.5m),
                new OrderDraftLineInput("Frame", 1m, "EA", 40m),
                new OrderDraftLineInput("Handle", 4m, "BOX", 3.25m),
                new OrderDraftLineInput("Cable", 3m, "M", 1.1m),
            ]));
}
