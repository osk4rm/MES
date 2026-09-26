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

        // Newest boundary first by default (RawSort initializer); callers can
        // override via RawSort. Filtering, ordering and paging are applied by
        // the repository through the paginator — no in-memory rework here, so
        // paged results stay consistent with totalCount.
        var paginator = new Paginator<ShiftHandover>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        var mapped = items
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
