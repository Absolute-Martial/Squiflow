using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Profiles;

namespace Application.Profiles.Postgres;

internal sealed class PublishedTenantPolicyJsonConverter : JsonConverter<PublishedTenantPolicy>
{
    public override PublishedTenantPolicy Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
            if (!names.Add(property.Name)) throw new JsonException();

        var hasVersion = root.TryGetProperty(nameof(PublishedTenantPolicy.Version), out var versionValue);
        if (!hasVersion)
        {
            if (root.TryGetProperty(nameof(PublishedTenantPolicy.RequireApprovalForProgramOrders), out _)) throw new JsonException();
            var legacy = JsonSerializer.Deserialize<LegacyPolicyFacts>(root.GetRawText(), options) ?? throw new JsonException();
            return new(legacy.PolicyRevisionId, legacy.TenantId, legacy.Revision, legacy.RequireReferenceForProgramOrders,
                legacy.PublishedByAccountId, legacy.PublishedAt, legacy.ObservedAuthorizationRevision,
                TenantProfileRules.LegacyPolicyVersion, false);
        }

        if (versionValue.ValueKind != JsonValueKind.String || versionValue.GetString() != TenantProfileRules.SupportedPolicyVersion ||
            !root.TryGetProperty(nameof(PublishedTenantPolicy.RequireApprovalForProgramOrders), out _)) throw new JsonException();
        var current = JsonSerializer.Deserialize<CurrentPolicyFacts>(root.GetRawText(), options) ?? throw new JsonException();
        return new(current.PolicyRevisionId, current.TenantId, current.Revision, current.RequireReferenceForProgramOrders,
            current.PublishedByAccountId, current.PublishedAt, current.ObservedAuthorizationRevision,
            current.Version, current.RequireApprovalForProgramOrders);
    }

    public override void Write(Utf8JsonWriter writer, PublishedTenantPolicy value, JsonSerializerOptions options)
    {
        if (value.Version == TenantProfileRules.LegacyPolicyVersion)
            JsonSerializer.Serialize(writer, new LegacyPolicyFacts(value.PolicyRevisionId, value.TenantId, value.Revision,
                value.RequireReferenceForProgramOrders, value.PublishedByAccountId, value.PublishedAt, value.ObservedAuthorizationRevision), options);
        else if (value.Version == TenantProfileRules.SupportedPolicyVersion)
            JsonSerializer.Serialize(writer, new CurrentPolicyFacts(value.PolicyRevisionId, value.TenantId, value.Revision,
                value.RequireReferenceForProgramOrders, value.PublishedByAccountId, value.PublishedAt,
                value.ObservedAuthorizationRevision, value.Version, value.RequireApprovalForProgramOrders), options);
        else
            throw new JsonException();
    }

    private sealed record LegacyPolicyFacts(Guid PolicyRevisionId, Guid TenantId, long Revision, bool RequireReferenceForProgramOrders,
        Guid PublishedByAccountId, DateTimeOffset PublishedAt, long ObservedAuthorizationRevision);

    private sealed record CurrentPolicyFacts(Guid PolicyRevisionId, Guid TenantId, long Revision, bool RequireReferenceForProgramOrders,
        Guid PublishedByAccountId, DateTimeOffset PublishedAt, long ObservedAuthorizationRevision,
        [property: JsonRequired] string Version, [property: JsonRequired] bool RequireApprovalForProgramOrders);
}
