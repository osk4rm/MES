using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Browse;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Start;

internal sealed class StartMaintenanceWorkOrderRequestHandler(
    IMaintenanceWorkOrdersRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<StartMaintenanceWorkOrderRequest, MaintenanceWorkOrderResponse>
{
    public async Task<MaintenanceWorkOrderResponse> Handle(
        StartMaintenanceWorkOrderRequest request, CancellationToken cancellationToken)
    {
        var workOrder = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("MaintenanceWorkOrder", request.Id);

        if (workOrder.Status != MaintenanceWorkOrderStatus.Open)
            throw new ValidationException(nameof(workOrder.Status),
                $"Only Open work orders can be started (current status: {workOrder.Status}).");

        workOrder.Status = MaintenanceWorkOrderStatus.InProgress;
        workOrder.StartedAt = dateTimeProvider.UtcNow;

        await repository.UpdateAsync(workOrder, cancellationToken);
        return BrowseMaintenanceWorkOrdersRequestHandler.Map(workOrder);
    }
}
