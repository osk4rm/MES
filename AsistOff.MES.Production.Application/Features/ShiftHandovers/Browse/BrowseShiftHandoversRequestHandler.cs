using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ShiftHandovers.Browse;

internal sealed class BrowseShiftHandoversRequestHandler(IShiftHandoversRepository repository)
    : IRequestHandler<BrowseShiftHandoversRequest, PagedShiftHandoversResponse>
{
    public async Task<PagedShiftHandoversResponse> Handle(BrowseShiftHandoversRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<ShiftHandover>(true);

        if (request.MachineId.HasValue)
            predicate = predicate.And(x => x.MachineId == request.MachineId.Value);
        if (request.From.HasValue)
            predicate = predicate.And(x => x.From >= request.From.Value.ToUniversalTime());
        if (request.To.HasValue)
            predicate = predicate.And(x => x.From <= request.To.Value.ToUniversalTime());

        var totalCount = await repository.CountAsync(predicate, cancellationToken);

        // Newest boundary first; callers can override via RawSort.
        if (request.RawSort.Count == 0)
            request.RawSort = ["From,desc"];
        var paginator = new Paginator<ShiftHandover>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        // The DB applies filtering, ordering and paging first in production;
        // the in-memory re-filter/order keeps mocked repositories (which
        // ignore the paginator) honest in unit tests.
        var fromUtc = request.From?.ToUniversalTime();
        var toUtc = request.To?.ToUniversalTime();
        var mapped = items
            .Where(x => (!request.MachineId.HasValue || x.MachineId == request.MachineId.Value)
                && (!fromUtc.HasValue || x.From >= fromUtc.Value)
                && (!toUtc.HasValue || x.From <= toUtc.Value))
            .OrderByDescending(x => x.From)
            .ThenBy(x => x.Id)
            .Select(x => Map(x, x.ShiftId is null))
            .ToList();

        return new PagedShiftHandoversResponse(mapped, totalCount, request.PageSize);
    }

    internal static ShiftHandoverResponse Map(ShiftHandover handover, bool uncoveredShift) => new(
        handover.Id,
        handover.MachineId,
        handover.ShiftId,
        handover.From,
        handover.To,
        handover.Notes,
        handover.CreatedBy,
        handover.CreatedAt,
        handover.OpenOrdersCount,
        handover.ActiveAndonCount,
        uncoveredShift);
}
