using System.Linq.Expressions;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Contracts.Sorting;
using System.Linq.Dynamic.Core;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Shared.Abstractions.Extensions;

public static class QueryableExtensions
{
    public static IQueryable<T> PageFilter<T>(this IQueryable<T> source, Paginator<T> paginator) where T : class, IEntity
    {
        return source
            .Where(paginator.Filter)
            .Sort(paginator.Paging)
            .Page(paginator.Paging);
    }
    
    public static IQueryable<T> Page<T>(this IQueryable<T> source, IPagedRequest pageRequest) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!pageRequest.PageNumber.HasValue || !pageRequest.PageSize.HasValue)
        {
            return source;
        }

        int skip = (pageRequest.PageNumber.Value - 1) * pageRequest.PageSize.Value;
        int take = pageRequest.PageSize.Value;

        return source.Skip(skip).Take(take);
    }

    public static IQueryable<T> Sort<T>(this IQueryable<T> source, ISortable sortRequest) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        var sortByFields = SortableResolver.ResolveSortFields(sortRequest.RawSort);
        if (sortByFields.Count == 0)
            return source;

        IOrderedQueryable<T> orderedResult = null;
        foreach (var sortField in sortByFields)
        {
            if (orderedResult == null)
            {
                orderedResult = sortField.Order == SortOrder.Ascending
                    ? source.OrderByDynamic(sortField.Field)
                    : source.OrderByDescendingDynamic(sortField.Field);
            }
            else
            {
                orderedResult = sortField.Order == SortOrder.Ascending
                    ? orderedResult.ThenByDynamic(sortField.Field)
                    : orderedResult.ThenByDescendingDynamic(sortField.Field);
            }
        }
        return orderedResult ?? source;
    }

    public static IQueryable<T> Filter<T>(this IQueryable<T> source, ExpressionStarter<T> filter) where T : class
    {
        return source.Where(filter);
    }

    public static IQueryable<T> WhereIf<T>(this IQueryable<T> source, bool condition, Expression<Func<T, bool>> predicate)
    {
        return condition ? source.Where(predicate) : source;
    }

    private static IOrderedQueryable<T> OrderByDynamic<T>(this IQueryable<T> source, string property)
        => source.OrderBy(property);
    private static IOrderedQueryable<T> OrderByDescendingDynamic<T>(this IQueryable<T> source, string property)
        => source.OrderBy($"{property} descending");
    private static IOrderedQueryable<T> ThenByDynamic<T>(this IOrderedQueryable<T> source, string property)
        => source.ThenBy(property);
    private static IOrderedQueryable<T> ThenByDescendingDynamic<T>(this IOrderedQueryable<T> source, string property)
        => source.ThenBy($"{property} descending");
}
