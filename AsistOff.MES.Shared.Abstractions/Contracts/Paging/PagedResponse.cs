namespace AsistOff.MES.Shared.Abstractions.Contracts.Paging;

public abstract class PagedResponse<T> : IPagedResponse<T>
{
    public int TotalCount { get; }
    public int TotalPages { get; }
    public IReadOnlyCollection<T> Items { get; }

    protected PagedResponse(IReadOnlyCollection<T> items, int totalCount, int? pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        if (pageSize is not > 0)
        {
            TotalPages = totalCount > 0 ? 1 : 0;
        }
        else
        {
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize.Value);
        }
    }
}