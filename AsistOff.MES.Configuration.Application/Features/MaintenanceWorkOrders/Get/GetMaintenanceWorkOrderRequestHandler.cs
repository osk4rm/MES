using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Browse;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Get;

internal sealed class GetMaintenanceWorkOrderRequestHandler(IMaintenanceWorkOrdersRepository repository)
    : IRequestHandler<GetMaintenanceWorkOrderRequest, MaintenanceWorkOrderResponse>
{
    public async Task<MaintenanceWorkOrderResponse> Handle(
        GetMaintenanceWorkOrderRequest request, CancellationToken cancellationToken)
    {
        var workOrder = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("MaintenanceWorkOrder", request.Id);
        return BrowseMaintenanceWorkOrdersRequestHandler.Map(workOrder);
    }
}
