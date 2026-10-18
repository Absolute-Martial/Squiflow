using System.Text;
using System.Text.Json.Serialization;

namespace Application.Web.Business;

public sealed record FulfillmentReceipt([property: JsonRequired] Guid WorkId, [property: JsonRequired] Guid OrderId,
    [property: JsonRequired] long Revision, string Stage);
public sealed record FulfillmentRecord([property: JsonRequired] Guid WorkId, [property: JsonRequired] Guid OrderId,
    [property: JsonRequired] long Revision, string Stage, [property: JsonRequired] bool Started,
    [property: JsonRequired] decimal OrderedQuantity, [property: JsonRequired] decimal CompletedQuantity,
    [property: JsonRequired] decimal HandedOffQuantity, string UnitCode, [property: JsonRequired] int UnitPrecision,
    Guid? ActiveDispositionId, string? ActiveDispositionKind, [property: JsonRequired] DateTimeOffset CreatedAt,
    [property: JsonRequired] DateTimeOffset UpdatedAt);
public sealed record FulfillmentListRecord([property: JsonRequired] Guid WorkId, [property: JsonRequired] Guid OrderId,
    [property: JsonRequired] long Revision, string Stage, [property: JsonRequired] decimal OrderedQuantity,
    [property: JsonRequired] decimal CompletedQuantity, [property: JsonRequired] decimal HandedOffQuantity,
    string UnitCode, [property: JsonRequired] DateTimeOffset CreatedAt);
public sealed record FulfillmentReplacement(string Kind, decimal? Quantity, string? Recipient, string? Disposition);
public sealed record FulfillmentEventRecord([property: JsonRequired] Guid EventId, [property: JsonRequired] long Revision,
    [property: JsonRequired] Guid ActorAccountId, [property: JsonRequired] DateTimeOffset RecordedAt,
    [property: JsonRequired] long AuthorizationRevision, string Kind, decimal? Quantity, string? Recipient, string? Reason,
    string? Disposition, Guid? TargetEventId, FulfillmentReplacement? Replacement, [property: JsonRequired] bool IsActive);
public sealed record OperationsPage<T>(T[] Items, string? NextCursor);
public sealed record OperationsHistory<T>([property: JsonRequired] long ObservedRevision, T[] Items, long? NextRevision);

public static class OperationsContracts
{
    public static bool Id(string? value, out Guid id) => Guid.TryParse(value, out id) && id != Guid.Empty;
    public static bool Revision(long value) => value is >= 0 and < long.MaxValue - 1;
    public static bool Text(string? value, int maximum, bool required = true)
    {
        if (value is null) return !required;
        if (value.Any(char.IsControl)) return false;
        value = value.Trim();
        var count = 0;
        for (var i = 0; i < value.Length;)
        {
            if (!Rune.TryGetRuneAt(value, i, out var rune) || Rune.IsControl(rune) || ++count > maximum) return false;
            i += rune.Utf16SequenceLength;
        }
        return !required || count > 0;
    }
    public static bool Quantity(decimal value, int precision = 4) => value > 0 && precision is >= 0 and <= 4 && decimal.Round(value, precision) == value;
    public static bool Money(decimal value, bool positive = false) => value >= (positive ? .01m : 0m) &&
        value <= 99999999999999999.99m && ((decimal.GetBits(value)[3] >> 16) & 255) <= 2;
    public static bool Aggregate(decimal value) => value >= 0 && ((decimal.GetBits(value)[3] >> 16) & 255) <= 2;
    public static bool Cursor(string? value) => value is null || value.Length is > 0 and <= 160 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    public static bool Page<T>(OperationsPage<T> page, Func<T, bool> validate, Func<T, Guid> id, string? previous) where T : class =>
        page.Items is { Length: <= 25 } && Cursor(page.NextCursor) && (page.NextCursor is null || page.Items.Length > 0 && page.NextCursor != previous) &&
        page.Items.All(x => x is not null && validate(x)) && page.Items.Select(id).Distinct().Count() == page.Items.Length;
    public static string HistoryPath(string path, long? before) => path + "/history?limit=10" +
        (before is { } revision ? "&beforeRevision=" + revision.ToString(System.Globalization.CultureInfo.InvariantCulture) : "");
    public static bool History<T>(OperationsHistory<T> page, Func<T, bool> validate, Func<T, long> revision, long? before) where T : class =>
        page.ObservedRevision > 0 && page.Items is { Length: <= 10 } &&
        page.Items.All(x => x is not null && validate(x) && revision(x) <= page.ObservedRevision && (!before.HasValue || revision(x) < before)) &&
        page.Items.Zip(page.Items.Skip(1)).All(pair => revision(pair.First) > revision(pair.Second)) &&
        (page.NextRevision is null || page.Items.Length > 0 && page.NextRevision > 0 && page.NextRevision == revision(page.Items[^1]));
}

public static class FulfillmentContracts
{
    public static bool Stage(string? value) => value is "NotStarted" or "InProgress" or "Paused" or "ReadyForPickup" or "HandedOff";
    public static bool Disposition(string? value) => value is "Stopped" or "Disputed" or "Defective";
    public static bool Receipt(FulfillmentReceipt value, Guid order, long revision) => value.OrderId == order && value.WorkId != Guid.Empty && value.Revision == revision && Stage(value.Stage);
    public static bool Record(FulfillmentRecord value, Guid order) => value.OrderId == order && value.WorkId != Guid.Empty && value.Revision > 0 && Stage(value.Stage) &&
        OperationsContracts.Quantity(value.OrderedQuantity, value.UnitPrecision) && value.CompletedQuantity >= 0 && value.HandedOffQuantity >= 0 &&
        value.CompletedQuantity <= value.OrderedQuantity && value.HandedOffQuantity <= value.CompletedQuantity && OperationsContracts.Text(value.UnitCode, 64) &&
        value.CreatedAt != default && value.UpdatedAt != default && (value.ActiveDispositionId is null ? value.ActiveDispositionKind is null : value.ActiveDispositionId != Guid.Empty && Disposition(value.ActiveDispositionKind));
    public static bool List(FulfillmentListRecord value) => value.OrderId != Guid.Empty && value.WorkId != Guid.Empty && value.Revision > 0 && Stage(value.Stage) &&
        OperationsContracts.Quantity(value.OrderedQuantity) && value.CompletedQuantity >= 0 && value.CompletedQuantity <= value.OrderedQuantity &&
        value.HandedOffQuantity >= 0 && value.HandedOffQuantity <= value.CompletedQuantity && OperationsContracts.Text(value.UnitCode, 64) && value.CreatedAt != default;
    public static bool Event(FulfillmentEventRecord value) => value.EventId != Guid.Empty && value.Revision > 0 && value.ActorAccountId != Guid.Empty && value.RecordedAt != default &&
        value.AuthorizationRevision > 0 && value.Kind is "Started" or "Completion" or "Handoff" or "Disposition" or "Resolution" or "Correction" &&
        (!value.Quantity.HasValue || OperationsContracts.Quantity(value.Quantity.Value)) && OperationsContracts.Text(value.Recipient, 128, false) &&
        OperationsContracts.Text(value.Reason, 512, false) && (value.Disposition is null || Disposition(value.Disposition)) && value.TargetEventId != Guid.Empty &&
        (value.Replacement is null || Replacement(value.Replacement));
    public static bool Replacement(FulfillmentReplacement value) => value.Kind switch
    {
        "Completion" => value.Quantity.HasValue && OperationsContracts.Quantity(value.Quantity.Value) && value.Recipient is null && value.Disposition is null,
        "Handoff" => value.Quantity.HasValue && OperationsContracts.Quantity(value.Quantity.Value) && OperationsContracts.Text(value.Recipient, 128) && value.Disposition is null,
        "Disposition" => (value.Quantity is null || OperationsContracts.Quantity(value.Quantity.Value)) && value.Recipient is null && Disposition(value.Disposition),
        _ => false,
    };
}
