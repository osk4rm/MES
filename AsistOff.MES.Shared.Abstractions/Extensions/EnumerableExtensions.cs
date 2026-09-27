using System.Linq.Expressions;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Contracts.Sorting;
using System.Linq.Dynamic.Core;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Exceptions;
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

    /// <summary>
    /// Applies client-requested ordering. Defense in depth for issue #311:
    /// every raw entry must match the strict <c>Field[,asc|desc]</c> grammar
    /// and -- when the request exposes a non-empty
    /// <see cref="ISortable.SupportedSortFields"/> whitelist -- the field must
    /// be whitelisted (case-insensitive). Anything else throws
    /// <see cref="ValidationException"/> (HTTP 400) before any Dynamic LINQ
    /// <c>OrderBy</c>/<c>ThenBy</c> executes, so attacker input can never reach
    /// the expression parser even if request validation is bypassed.
    /// Requests with an empty whitelist are server-owned paging (e.g. capped
    /// typeahead or enrichment lookups): the field must then be a real
    /// readable property of <typeparamref name="T"/>.
    /// </summary>
    public static IQueryable<T> Sort<T>(this IQueryable<T> source, ISortable sortRequest) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sortRequest);

        if (sortRequest.RawSort == null || sortRequest.RawSort.Count == 0)
            return source;

        var whitelist = sortRequest.SupportedSortFields;
        var sortByFields = new List<(string Field, SortOrder Order)>();

        foreach (var raw in sortRequest.RawSort)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || parts.Length > 2)
                throw new ValidationException("RawSort", $"Sort parameter '{raw}' must be 'Field' or 'Field,asc|desc'.");

            var field = parts[0];
            if (!SortableResolver.IsValidFieldName(field))
                throw new ValidationException("RawSort", $"Sort field '{field}' must be supported.");

            SortOrder order = SortOrder.Ascending;
            if (parts.Length == 2)
            {
                var token = parts[1];
                if (token.Equals("desc", StringComparison.OrdinalIgnoreCase))
                    order = SortOrder.Descending;
                else if (token.Equals("asc", StringComparison.OrdinalIgnoreCase))
                    order = SortOrder.Ascending;
                else
                    throw new ValidationException("RawSort", "Invalid sort order");
            }

            string canonical;
            if (whitelist is not null && whitelist.Count != 0)
            {
                var match = whitelist.FirstOrDefault(s => s.Equals(field, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                    throw new ValidationException("RawSort", $"Sort field '{field}' must be supported.");
                canonical = match;
            }
            else
            {
                var property = typeof(T).GetProperty(
                    field,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (property is null || !property.CanRead)
                    throw new ValidationException("RawSort", $"Sort field '{field}' must be supported.");
                canonical = property.Name;
            }

            sortByFields.Add((canonical, order));
        }

        if (sortByFields.Count == 0)
            return source;

        IOrderedQueryable<T>? orderedResult = null;
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
    {
        if (!SortableResolver.IsValidFieldName(property))
            throw new ValidationException("RawSort", $"Sort field '{property}' must be supported.");
        return source.OrderBy(property);
    }

    private static IOrderedQueryable<T> OrderByDescendingDynamic<T>(this IQueryable<T> source, string property)
    {
        if (!SortableResolver.IsValidFieldName(property))
            throw new ValidationException("RawSort", $"Sort field '{property}' must be supported.");
        return source.OrderBy($"{property} descending");
    }

    private static IOrderedQueryable<T> ThenByDynamic<T>(this IOrderedQueryable<T> source, string property)
    {
        if (!SortableResolver.IsValidFieldName(property))
            throw new ValidationException("RawSort", $"Sort field '{property}' must be supported.");
        return source.ThenBy(property);
    }

    private static IOrderedQueryable<T> ThenByDescendingDynamic<T>(this IOrderedQueryable<T> source, string property)
    {
        if (!SortableResolver.IsValidFieldName(property))
            throw new ValidationException("RawSort", $"Sort field '{property}' must be supported.");
        return source.ThenBy($"{property} descending");
    }
}
