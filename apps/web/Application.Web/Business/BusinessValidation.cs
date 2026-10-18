namespace Application.Web.Business;

public static class BusinessValidation
{
    public static bool Text(string? value, int maximum, bool required = true) =>
        value is null ? !required : value.Length <= maximum && !value.Any(char.IsControl) && (!required || !string.IsNullOrWhiteSpace(value));

    public static bool Cursor(string? value) => value is null || value.Length is > 0 and <= 192 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public static bool Page<T>(T[]? items, string? cursor, Func<T, bool> validate, Func<T, Guid> id) where T : class =>
        items is { Length: <= 25 } && Cursor(cursor) && (cursor is null || items.Length > 0) &&
        items.All(item => item is not null && validate(item)) && items.Select(id).Distinct().Count() == items.Length;

    public static string PagePath(string resource, string? after) => resource + "?limit=25" + (after is null ? "" : "&after=" + Uri.EscapeDataString(after));
}
