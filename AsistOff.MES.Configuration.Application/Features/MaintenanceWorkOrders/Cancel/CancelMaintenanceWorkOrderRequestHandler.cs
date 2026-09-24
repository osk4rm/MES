using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Browse;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Cancel;

internal sealed class CancelMaintenanceWorkOrderRequestHandler(IMaintenanceWorkOrdersRepository repository)
    : IRequestHandler<CancelMaintenanceWorkOrderRequest, MaintenanceWorkOrderResponse>
{
    public async Task<MaintenanceWorkOrderResponse> Handle(
        CancelMaintenanceWorkOrderRequest request, CancellationToken cancellationToken)
    {
        var workOrder = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("MaintenanceWorkOrder", request.Id);

        if (workOrder.Status is not (MaintenanceWorkOrderStatus.Open or MaintenanceWorkOrderStatus.InProgress))
            throw new ValidationException(nameof(workOrder.Status),
                $"Only Open or InProgress work orders can be cancelled (current status: {workOrder.Status}).");

        workOrder.Status = MaintenanceWorkOrderStatus.Cancelled;

        await repository.UpdateAsync(workOrder, cancellationToken);
        return BrowseMaintenanceWorkOrdersRequestHandler.Map(workOrder);
    }
}
