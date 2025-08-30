namespace AsistOff.MES.Shared.Abstractions.Contracts.Sorting;

public interface ISortable
{
    List<string> RawSort { get; set; }
    IReadOnlyCollection<string> SupportedSortFields { get; }
}