using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Browse;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Complete;

internal sealed class CompleteMaintenanceWorkOrderRequestHandler(
    IMaintenanceWorkOrdersRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CompleteMaintenanceWorkOrderRequest, MaintenanceWorkOrderResponse>
{
    public async Task<MaintenanceWorkOrderResponse> Handle(
        CompleteMaintenanceWorkOrderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ResolutionNotes))
            throw new ValidationException(nameof(request.ResolutionNotes), "Resolution notes are required");

        var workOrder = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("MaintenanceWorkOrder", request.Id);

        if (workOrder.Status is not (MaintenanceWorkOrderStatus.Open or MaintenanceWorkOrderStatus.InProgress))
            throw new ValidationException(nameof(workOrder.Status),
                $"Only Open or InProgress work orders can be completed (current status: {workOrder.Status}).");

        workOrder.Status = MaintenanceWorkOrderStatus.Done;
        workOrder.ResolutionNotes = request.ResolutionNotes;
        workOrder.CompletedAt = dateTimeProvider.UtcNow;

        await repository.UpdateAsync(workOrder, cancellationToken);
        return BrowseMaintenanceWorkOrdersRequestHandler.Map(workOrder);
    }
}
