namespace AsistOff.MES.Shared.Abstractions.Contracts.Paging;

public interface IPagedResponse <out T> : ICollectionResponse<T>
{
    int TotalCount { get; }
    int TotalPages { get; }
}