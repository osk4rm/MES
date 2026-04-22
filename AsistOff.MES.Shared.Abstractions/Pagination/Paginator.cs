using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Contracts.Sorting;
using AsistOff.MES.Shared.Abstractions.DAL;
using LinqKit;

namespace AsistOff.MES.Shared.Abstractions.Pagination;

public class Paginator<T> where T : IEntity
{
    public ExpressionStarter<T> Filter { get; }
    public IPagedRequest Paging { get; }

    public Paginator(ExpressionStarter<T> filter, IPagedRequest paging)
    {
        Filter = filter;
        Paging = paging;
    }
}