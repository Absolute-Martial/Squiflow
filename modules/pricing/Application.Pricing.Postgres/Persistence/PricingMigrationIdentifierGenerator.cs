using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Pricing.Postgres;

public sealed class PricingMigrationIdentifierGenerator : IMigrationsIdGenerator
{
    public string GenerateId(string name) =>
        throw new NotSupportedException("Pricing migration identifiers are hand-authored and cannot be scaffolded.");

    public string GetName(string id)
    {
        var separator = id.IndexOf('_');
        return separator >= 0 && separator < id.Length - 1 ? id[(separator + 1)..] : id;
    }

    public bool IsValidId(string value) =>
        value.Length > 13 && value[12] == '_' && value[..12].All(character => character is >= '0' and <= '9');
}
