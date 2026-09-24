using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Browse;

internal sealed class BrowseMaintenanceWorkOrdersRequestHandler(IMaintenanceWorkOrdersRepository repository)
    : IRequestHandler<BrowseMaintenanceWorkOrdersRequest, PagedResponse<MaintenanceWorkOrderResponse>>
{
    public async Task<PagedResponse<MaintenanceWorkOrderResponse>> Handle(
        BrowseMaintenanceWorkOrdersRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<MaintenanceWorkOrder>(true);

        if (request.MachineId.HasValue)
            predicate = predicate.And(x => x.MachineId == request.MachineId.Value);
        if (request.Status.HasValue)
            predicate = predicate.And(x => x.Status == request.Status.Value);

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<MaintenanceWorkOrder>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        var result = items.Select(Map).ToList();
        return new PagedMaintenanceWorkOrdersResponse(result, totalCount, request.PageSize);
    }

    internal static MaintenanceWorkOrderResponse Map(MaintenanceWorkOrder w) => new(
        w.Id, w.Code, w.Title, w.Description, w.MachineId, w.Machine?.Code,
        w.Priority, w.Status, w.ReportedAt, w.StartedAt, w.CompletedAt, w.ResolutionNotes);
}
