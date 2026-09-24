using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.LotGenealogy.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.LotGenealogy.Record;

internal sealed class RecordLotGenealogyEdgeRequestHandler(
    ILotGenealogyEdgesRepository edgesRepository,
    ILotsRepository lotsRepository,
    IProductionOrdersRepository ordersRepository,
    IProductionConfirmationsRepository confirmationsRepository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<RecordLotGenealogyEdgeRequest, LotGenealogyEdgeResponse>
{
    public async Task<LotGenealogyEdgeResponse> Handle(RecordLotGenealogyEdgeRequest request, CancellationToken cancellationToken)
    {
        if (request.ConsumedLotId == request.ProducedLotId)
            throw new ValidationException(nameof(request.ProducedLotId), "Consumed and produced lots must differ.");

        if (request.ConsumedQuantity <= 0)
            throw new ValidationException(nameof(request.ConsumedQuantity), "Consumed quantity must be greater than zero.");

        if (request.OccurredAt == default)
            throw new ValidationException(nameof(request.OccurredAt), "Occurred at is required.");

        var occurredAt = request.OccurredAt.ToUniversalTime();

        if (occurredAt > dateTimeProvider.UtcNow.AddMinutes(1))
            throw new ValidationException(nameof(request.OccurredAt), "Occurred at cannot be in the future.");

        if (request.MachineId == Guid.Empty)
            throw new ValidationException(nameof(request.MachineId), "Machine is required.");

        if (request.Notes is { Length: > 1000 })
            throw new ValidationException(nameof(request.Notes), "Notes cannot exceed 1000 characters.");

        var consumedLot = await lotsRepository.GetAsync(request.ConsumedLotId, cancellationToken)
            ?? throw new NotFoundException("Lot", request.ConsumedLotId);

        var producedLot = await lotsRepository.GetAsync(request.ProducedLotId, cancellationToken)
            ?? throw new NotFoundException("Lot", request.ProducedLotId);

        var order = await ordersRepository.GetAsync(request.ProductionOrderId, cancellationToken)
            ?? throw new NotFoundException("ProductionOrder", request.ProductionOrderId);

        ProductionConfirmation? confirmation = null;
        if (request.ProductionConfirmationId.HasValue)
        {
            confirmation = await confirmationsRepository.GetAsync(request.ProductionConfirmationId.Value, cancellationToken)
                ?? throw new NotFoundException("ProductionConfirmation", request.ProductionConfirmationId.Value);

            if (confirmation.ProductionOrderId != order.Id)
                throw new ValidationException(nameof(request.ProductionConfirmationId), "Confirmation must belong to the same production order.");
        }

        var edge = new LotGenealogyEdge
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            ConsumedLotId = consumedLot.Id,
            ProducedLotId = producedLot.Id,
            ProductionOrderId = order.Id,
            ProductionConfirmationId = confirmation?.Id,
            MachineId = request.MachineId,
            ReportedByOperatorId = request.ReportedByOperatorId,
            ConsumedQuantity = request.ConsumedQuantity,
            OccurredAt = occurredAt,
            Notes = request.Notes,
            CreatedAt = dateTimeProvider.UtcNow
        };

        await edgesRepository.AddAsync(edge, cancellationToken);

        return BrowseLotGenealogyEdgesRequestHandler.Map(edge);
    }
}
