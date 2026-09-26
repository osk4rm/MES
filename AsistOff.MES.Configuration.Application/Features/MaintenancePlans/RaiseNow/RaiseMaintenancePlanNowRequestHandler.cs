using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.EvaluateDue;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Browse;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.RaiseNow;

internal sealed class RaiseMaintenancePlanNowRequestHandler(
    IMaintenancePlansRepository plansRepository,
    IMaintenanceWorkOrdersRepository workOrdersRepository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<RaiseMaintenancePlanNowRequest, MaintenanceWorkOrderResponse>
{
    public async Task<MaintenanceWorkOrderResponse> Handle(
        RaiseMaintenancePlanNowRequest request, CancellationToken cancellationToken)
    {
        var plan = await plansRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("MaintenancePlan", request.Id);

        if (!plan.IsActive)
            throw new ValidationException(nameof(plan.IsActive), "Only active plans can raise work orders.");

        var openOrders = await workOrdersRepository.ListOpenByPlanIdsAsync(
            new[] { plan.Id }, cancellationToken);
        var existing = openOrders.FirstOrDefault(o => o.PlanId == plan.Id);
        if (existing is not null)
        {
            existing.Machine = plan.Machine;
            return BrowseMaintenanceWorkOrdersRequestHandler.Map(existing);
        }

        var nowUtc = dateTimeProvider.UtcNow;
        var orderId = guidProvider.NewGuid();
        var workOrder = new MaintenanceWorkOrder
        {
            Id = orderId,
            TenantId = tenantContext.TenantId,
            Code = await GenerateUniqueCodeAsync(plan.Code, orderId, cancellationToken),
            Title = EvaluateDueMaintenancePlansRequestHandler.BuildTitle(plan.Name),
            Description = $"Manually raised from preventive plan {plan.Code}.",
            MachineId = plan.MachineId,
            PlanId = plan.Id,
            Priority = MaintenanceWorkOrderPriority.Medium,
            Status = MaintenanceWorkOrderStatus.Open,
            ReportedAt = nowUtc,
        };

        await workOrdersRepository.AddAsync(workOrder, cancellationToken);
        workOrder.Machine = plan.Machine;
        return BrowseMaintenanceWorkOrdersRequestHandler.Map(workOrder);
    }

    private async Task<string> GenerateUniqueCodeAsync(
        string planCode, Guid orderId, CancellationToken cancellationToken)
    {
        var prefix = planCode.Length > 37 ? planCode[..37] : planCode;
        var first = $"{prefix}-WO-{orderId:N}"[..Math.Min(50, prefix.Length + 4 + 8)].ToUpperInvariant();
        if (!await workOrdersRepository.CodeExistsAsync(first, null, cancellationToken))
            return first;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var suffix = guidProvider.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var candidate = $"{prefix}-WO-{suffix}";
            if (!await workOrdersRepository.CodeExistsAsync(candidate, null, cancellationToken))
                return candidate;
        }

        return $"{prefix}-WO-{Guid.NewGuid():N}"[..50].ToUpperInvariant();
    }
}
