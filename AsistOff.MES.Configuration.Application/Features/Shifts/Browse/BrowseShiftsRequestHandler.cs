using AsistOff.MES.Configuration.Application.Features.Shifts.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Shifts.Browse;

internal sealed class BrowseShiftsRequestHandler(IShiftsRepository repository)
    : IRequestHandler<BrowseShiftsRequest, PagedResponse<ShiftResponse>>
{
    public async Task<PagedResponse<ShiftResponse>> Handle(BrowseShiftsRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<Shift>(true);

        if (!string.IsNullOrWhiteSpace(request.Name))
            predicate = predicate.And(x => x.Name.Contains(request.Name));
        if (!string.IsNullOrWhiteSpace(request.Code))
            predicate = predicate.And(x => x.Code.Contains(request.Code));
        if (request.IsActive.HasValue)
            predicate = predicate.And(x => x.IsActive == request.IsActive);

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<Shift>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        var result = items.Select(Map).ToList();
        return new PagedShiftsResponse(result, totalCount, request.PageSize);
    }

    internal static ShiftResponse Map(Shift s) => new(s.Id, s.Code, s.Name, s.Description, s.StartTime, s.EndTime, s.IsActive);
}
