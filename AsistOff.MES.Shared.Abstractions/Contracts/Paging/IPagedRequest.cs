using AsistOff.MES.Shared.Abstractions.Contracts.Sorting;

namespace AsistOff.MES.Shared.Abstractions.Contracts.Paging;

public interface IPagedRequest : ISortable
{
    int? PageNumber { get; }
    int? PageSize { get; }
    int? MaxPageSize { get; }
}