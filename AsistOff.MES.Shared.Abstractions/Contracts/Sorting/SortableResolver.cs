using System.Text.RegularExpressions;

namespace AsistOff.MES.Shared.Abstractions.Contracts.Sorting;

/// <summary>
/// Parses client-supplied <c>RawSort</c> entries of the form
/// <c>Field</c> or <c>Field,asc|desc</c> into <see cref="SortField"/> values.
/// Only strict identifier field names (<c>^[A-Za-z_][A-Za-z0-9_]*$</c>) and
/// the <c>asc</c>/<c>desc</c> order tokens are accepted; anything else is
/// dropped so it can never reach Dynamic LINQ. Whitelist enforcement against
/// <see cref="ISortable.SupportedSortFields"/> happens in the validators and
/// in <c>QueryableExtensions.Sort</c> (defense in depth, issue #311).
/// </summary>
public static class SortableResolver
{
    private static readonly Regex FieldNamePattern =
        new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public static bool IsValidFieldName(string? field) =>
        !string.IsNullOrEmpty(field) && FieldNamePattern.IsMatch(field);

    public static bool IsValidOrderToken(string? token) =>
        token is not null &&
        (token.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
         token.Equals("desc", StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyCollection<SortField> ResolveSortFields(IReadOnlyCollection<string>? rawSort)
    {
        if (rawSort == null)
            return Array.Empty<SortField>();

        var sortFields = new List<SortField>();

        foreach (var raw in rawSort)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            if (!TryParseSingle(raw, out var parsed) || parsed is null)
                continue;

            sortFields.Add(parsed);
        }

        return sortFields;
    }

    internal static bool TryParseSingle(string raw, out SortField? sortField)
    {
        sortField = null;

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Length > 2)
            return false;

        var field = parts[0];
        if (!IsValidFieldName(field))
            return false;

        var order = SortOrder.Ascending;
        if (parts.Length == 2)
        {
            var token = parts[1];
            if (token.Equals("desc", StringComparison.OrdinalIgnoreCase))
                order = SortOrder.Descending;
            else if (token.Equals("asc", StringComparison.OrdinalIgnoreCase))
                order = SortOrder.Ascending;
            else
                return false;
        }

        sortField = new SortField(field, order);
        return true;
    }
}
