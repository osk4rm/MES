using AsistOff.MES.Configuration.Application.Features.MaintenancePlans;
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
    IMaintenancePlansRepository plansRepository,
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

        // Preventive loop closure (slice 3/3): a plan-linked completion rolls
        // the plan forward. Standalone corrective orders leave plans untouched.
        // The lookup runs under the tenant global query filter, so a
        // cross-tenant PlanId surfaces as 404.
        if (workOrder.PlanId.HasValue)
        {
            var plan = await plansRepository.GetByIdAsync(workOrder.PlanId.Value, cancellationToken)
                ?? throw new NotFoundException("MaintenancePlan", workOrder.PlanId.Value);

            MaintenancePlanRollover.Apply(plan, workOrder.CompletedAt.Value);
            await plansRepository.UpdateAsync(plan, cancellationToken);
        }

        return BrowseMaintenanceWorkOrdersRequestHandler.Map(workOrder);
    }
}
