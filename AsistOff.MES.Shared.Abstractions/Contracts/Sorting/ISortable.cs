namespace AsistOff.MES.Shared.Abstractions.Contracts.Sorting;

public interface ISortable
{
    IReadOnlyCollection<string> RawSort { get; }
    IReadOnlyCollection<string> SupportedSortFields { get; }
}