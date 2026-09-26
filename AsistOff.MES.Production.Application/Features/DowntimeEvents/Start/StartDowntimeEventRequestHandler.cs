using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.DowntimeEvents.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Observability;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.DowntimeEvents.Start;

internal sealed class StartDowntimeEventRequestHandler(
    IDowntimeEventsRepository repository,
    IProductionOrdersRepository ordersRepository,
    IReasonCodesRepository reasonCodesRepository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<StartDowntimeEventRequest, DowntimeEventResponse>
{
    public async Task<DowntimeEventResponse> Handle(StartDowntimeEventRequest request, CancellationToken cancellationToken)
    {
        if (request.MachineId == Guid.Empty)
            throw new ValidationException(nameof(request.MachineId), "Machine is required.");
        if (request.ReasonCodeId == Guid.Empty)
            throw new ValidationException(nameof(request.ReasonCodeId), "Reason code is required.");
        if (request.StartedAt == default)
            throw new ValidationException(nameof(request.StartedAt), "Start time is required.");
        if (request.StartedAt > dateTimeProvider.UtcNow)
            throw new ValidationException(nameof(request.StartedAt), "Start time cannot be in the future.");

        if (await repository.HasOpenEventAsync(request.MachineId, null, cancellationToken))
            throw new ConflictException("The Work Center already has an open downtime event.");

        Guid? productionOrderId = null;
        if (request.ProductionOrderId.HasValue)
        {
            var order = await ordersRepository.GetAsync(request.ProductionOrderId.Value, cancellationToken)
                ?? throw new NotFoundException("ProductionOrder", request.ProductionOrderId.Value);

            if (order.Status is not (ProductionOrderStatus.Released or ProductionOrderStatus.InProgress))
                throw new ValidationException(
                    nameof(request.ProductionOrderId),
                    "Downtime can only be linked to Released or InProgress orders.");

            productionOrderId = order.Id;
        }

        var now = dateTimeProvider.UtcNow;
        var entity = new DowntimeEvent
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            MachineId = request.MachineId,
            ReasonCodeId = request.ReasonCodeId,
            StartedAt = request.StartedAt,
            EndedAt = null,
            Notes = request.Notes,
            ReportedByOperatorId = request.ReportedByOperatorId,
            ProductionOrderId = productionOrderId,
            CreatedAt = now
        };

        await repository.AddAsync(entity, cancellationToken);

        // Availability loss, success path only. Tenant-filtered reason lookup
        // keeps the label bounded and tenant-safe without touching the error
        // contract (see the scrap handler for the same rule).
        var reasonCode = await reasonCodesRepository.GetByIdAsync(request.ReasonCodeId, cancellationToken);
        MesMeters.RecordDowntime(request.MachineId, reasonCode?.Code, tenantContext.TenantId);
        return BrowseDowntimeEventsRequestHandler.Map(entity);
    }
}
