using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.IdentityAccess.Postgres;

// Repository migration IDs use yyyyMMddNNNN_<name>, not EF's 14-digit timestamp format.
internal sealed class IdentityMigrationIdentifierGenerator : IMigrationsIdGenerator
{
    private const int PrefixDigits = 12;

    public string GenerateId(string name) =>
        throw new NotSupportedException($"IdentityAccess migration IDs are hand-authored; scaffolding '{name}' is not supported.");

    public string GetName(string id)
    {
        var separator = id.IndexOf('_');
        return separator >= 0 && separator < id.Length - 1 ? id[(separator + 1)..] : id;
    }

    public bool IsValidId(string value)
    {
        if (value.Length <= PrefixDigits + 1 || value[PrefixDigits] != '_') return false;
        for (var index = 0; index < PrefixDigits; index++)
        {
            if (value[index] is < '0' or > '9') return false;
        }

        return true;
    }
}
