using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.ScrapEvents.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Observability;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ScrapEvents.Create;

internal sealed class CreateScrapEventRequestHandler(
    IScrapEventsRepository repository,
    IProductionOrdersRepository ordersRepository,
    IReasonCodesRepository reasonCodesRepository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateScrapEventRequest, ScrapEventResponse>
{
    public async Task<ScrapEventResponse> Handle(CreateScrapEventRequest request, CancellationToken cancellationToken)
    {
        if (request.MachineId == Guid.Empty)
            throw new ValidationException(nameof(request.MachineId), "Machine is required.");

        if (request.ReasonCodeId == Guid.Empty)
            throw new ValidationException(nameof(request.ReasonCodeId), "Reason code is required.");

        if (request.Quantity <= 0)
            throw new ValidationException(nameof(request.Quantity), "Quantity must be greater than zero.");

        if (request.ReportedAt == default)
            throw new ValidationException(nameof(request.ReportedAt), "Reported at is required.");

        if (request.ReportedAt.ToUniversalTime() > dateTimeProvider.UtcNow.AddMinutes(1))
            throw new ValidationException(nameof(request.ReportedAt), "Reported at cannot be in the future.");

        Guid? productionOrderId = null;
        if (request.ProductionOrderId.HasValue)
        {
            var order = await ordersRepository.GetAsync(request.ProductionOrderId.Value, cancellationToken)
                ?? throw new NotFoundException("ProductionOrder", request.ProductionOrderId.Value);

            if (order.Status is not (ProductionOrderStatus.Released or ProductionOrderStatus.InProgress))
                throw new ValidationException(
                    nameof(request.ProductionOrderId),
                    "Scrap can only be linked to Released or InProgress orders.");

            productionOrderId = order.Id;
        }

        var scrapEvent = new ScrapEvent
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            MachineId = request.MachineId,
            ReasonCodeId = request.ReasonCodeId,
            Quantity = request.Quantity,
            ReportedAt = request.ReportedAt.ToUniversalTime(),
            Notes = request.Notes,
            ReportedByOperatorId = request.ReportedByOperatorId,
            ProductionOrderId = productionOrderId,
            CreatedAt = dateTimeProvider.UtcNow
        };

        await repository.AddAsync(scrapEvent, cancellationToken);

        // Quality loss, success path only. The reason label resolves through
        // the tenant-filtered lookup (same rule as the OEE losses Pareto):
        // unresolvable ids collapse to the bounded "unknown" placeholder, so
        // the meter never leaks cross-tenant codes and never changes the
        // handler's error contract.
        var reasonCode = await reasonCodesRepository.GetByIdAsync(request.ReasonCodeId, cancellationToken);
        MesMeters.RecordScrap(request.MachineId, reasonCode?.Code, tenantContext.TenantId);
        return BrowseScrapEventsRequestHandler.Map(scrapEvent);
    }
}
