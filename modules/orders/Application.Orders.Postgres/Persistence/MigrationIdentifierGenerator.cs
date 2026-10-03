using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Orders.Postgres;

// Implements EF's migration identifier contract lookup against the repository's durable
// migration identifier format, yyyyMMddNNNN_<name>. EF's built-in generator only recognizes
// its own 14-digit yyyyMMddHHmmss prefix, which leaves every repository migration identifier
// unresolvable for id/name based migration targeting such as a deliberate down-migration.
internal sealed class MigrationIdentifierGenerator : IMigrationsIdGenerator
{
    private const int DateDigits = 12;

    public string GenerateId(string name)
        => throw new NotSupportedException(
            $"Migration identifiers are hand-authored capability contracts in yyyyMMddNNNN_<name> form; scaffolding '{name}' is not supported.");

    public string GetName(string id)
    {
        var separator = id.IndexOf('_');
        return separator >= 0 && separator < id.Length - 1 ? id[(separator + 1)..] : id;
    }

    public bool IsValidId(string value)
        => value.Length > DateDigits + 1
            && value[DateDigits] == '_'
            && HasDateDigits(value);

    private static bool HasDateDigits(string value)
    {
        for (var i = 0; i < DateDigits; i++)
        {
            if (value[i] is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}
