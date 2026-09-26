using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.EvaluateDue;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Browse;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.EvaluateDue;

internal sealed class EvaluateDueMaintenancePlansRequestHandler(
    IMaintenancePlansRepository plansRepository,
    IMaintenanceWorkOrdersRepository workOrdersRepository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<EvaluateDueMaintenancePlansRequest, IReadOnlyCollection<MaintenanceWorkOrderResponse>>
{
    public async Task<IReadOnlyCollection<MaintenanceWorkOrderResponse>> Handle(
        EvaluateDueMaintenancePlansRequest request, CancellationToken cancellationToken)
    {
        var nowUtc = dateTimeProvider.UtcNow;
        var effectiveReading = request.CurrentMeterReading ?? request.MeterReading;

        var plans = await plansRepository.ListActiveAsync(cancellationToken);
        if (plans.Count == 0)
            return Array.Empty<MaintenanceWorkOrderResponse>();

        var planIds = plans.Select(p => p.Id).ToList();
        var openOrders = await workOrdersRepository.ListOpenByPlanIdsAsync(planIds, cancellationToken);
        var guardedPlanIds = new HashSet<Guid>(
            openOrders.Where(o => o.PlanId.HasValue).Select(o => o.PlanId!.Value));

        var raised = new List<MaintenanceWorkOrderResponse>();
        foreach (var plan in plans)
        {
            if (guardedPlanIds.Contains(plan.Id))
                continue;

            var meterReading = MaintenancePlanDueEvaluator.ResolveMeterReading(
                plan, effectiveReading, request.MeterReadings);

            if (!MaintenancePlanDueEvaluator.IsDue(plan, nowUtc, meterReading))
                continue;

            var orderId = guidProvider.NewGuid();
            var workOrder = new MaintenanceWorkOrder
            {
                Id = orderId,
                TenantId = tenantContext.TenantId,
                Code = await GenerateUniqueCodeAsync(plan.Code, orderId, cancellationToken),
                Title = BuildTitle(plan.Name),
                Description = $"Auto-raised from preventive plan {plan.Code}.",
                MachineId = plan.MachineId,
                PlanId = plan.Id,
                Priority = MaintenanceWorkOrderPriority.Medium,
                Status = MaintenanceWorkOrderStatus.Open,
                ReportedAt = nowUtc,
            };

            await workOrdersRepository.AddAsync(workOrder, cancellationToken);
            workOrder.Machine = plan.Machine;
            raised.Add(BrowseMaintenanceWorkOrdersRequestHandler.Map(workOrder));
        }

        return raised;
    }

    internal static string BuildTitle(string planName)
    {
        const string prefix = "Preventive maintenance ";
        var title = prefix + planName;
        return title.Length > 200 ? title[..200] : title;
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

        // Practically unreachable; fall back to a full guid suffix.
        return $"{prefix}-WO-{Guid.NewGuid():N}"[..50].ToUpperInvariant();
    }
}
