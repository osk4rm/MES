namespace AsistOff.MES.Shared.Abstractions.Contracts.Sorting;

internal static class SortableResolver
{
    private const int FieldNameIndex = 0;
    private const int SortingOrderIndex = 1;

    public static IReadOnlyCollection<SortField> ResolveSortFields(IReadOnlyCollection<string>? rawSort)
    {
        var sortFields = new List<SortField>();

        if (rawSort == null)
        {
            return sortFields;
        }
        
        foreach (var raw in rawSort)
        {
            var parts = raw.Split(',');
            var field = parts[FieldNameIndex].Trim();

            var order = (SortOrder)Enum.Parse(typeof(SortOrder), parts[SortingOrderIndex].Trim(), true);

            sortFields.Add(new SortField(field, order));
        }

        return sortFields;
    }
}