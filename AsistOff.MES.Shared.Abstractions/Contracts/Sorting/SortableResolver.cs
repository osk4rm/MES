namespace AsistOff.MES.Shared.Abstractions.Contracts.Sorting;

internal static class SortableResolver
{
    public static IReadOnlyCollection<SortField> ResolveSortFields(IReadOnlyCollection<string>? rawSort)
    {
        if (rawSort == null)
            return Array.Empty<SortField>();

        var sortFields = new List<SortField>();

        foreach (var raw in rawSort)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
                continue;

            var order = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase)
                ? SortOrder.Descending
                : SortOrder.Ascending;

            sortFields.Add(new SortField(parts[0], order));
        }

        return sortFields;
    }
}