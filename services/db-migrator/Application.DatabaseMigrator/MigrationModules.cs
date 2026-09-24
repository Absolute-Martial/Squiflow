using Microsoft.EntityFrameworkCore;
using Application.IdentityAccess.Postgres;
using Application.Orders.Postgres;
using Application.Tenancy.Postgres;

namespace Application.DatabaseMigrator;

internal sealed record MigrationModule(
    string Name,
    int Order,
    Type DbContextType,
    Func<string, DbContext> CreateContext);

internal static class MigrationModules
{
    internal static IReadOnlyList<MigrationModule> All { get; } = ValidateAndOrder(
    [
        new("identity-access", 100, typeof(IdentityAccessDbContext), IdentityAccessPostgresMigrations.CreateContext),
        new("tenancy", 200, typeof(TenancyDbContext), TenancyPostgresMigrations.CreateContext),
        new("orders", 300, typeof(OrderDbContext), OrdersPostgresMigrations.CreateContext),
    ]);

    internal static IReadOnlyList<MigrationModule> ValidateAndOrder(IEnumerable<MigrationModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        var ordered = modules.OrderBy(module => module.Order).ToArray();
        if (ordered.Length == 0 ||
            ordered.Any(module => string.IsNullOrWhiteSpace(module.Name) ||
                module.Order <= 0 ||
                !typeof(DbContext).IsAssignableFrom(module.DbContextType) ||
                module.CreateContext is null) ||
            ordered.Select(module => module.Name).Distinct(StringComparer.Ordinal).Count() != ordered.Length ||
            ordered.Select(module => module.Order).Distinct().Count() != ordered.Length ||
            ordered.Select(module => module.DbContextType).Distinct().Count() != ordered.Length)
        {
            throw new InvalidOperationException("Migration modules require unique names, orders and DbContext types.");
        }

        return Array.AsReadOnly(ordered);
    }
}
