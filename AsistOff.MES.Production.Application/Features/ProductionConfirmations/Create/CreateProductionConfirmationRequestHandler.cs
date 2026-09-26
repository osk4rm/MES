using System.Diagnostics;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Observability;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionConfirmations.Create;

internal sealed class CreateProductionConfirmationRequestHandler(
    IProductionConfirmationsRepository confirmationsRepository,
    IProductionOrdersRepository ordersRepository,
    IChildEntitiesRepository childEntitiesRepository,
    IStockMovementsRepository stockMovementsRepository,
    ILotsRepository lotsRepository,
    ILotGenealogyEdgesRepository genealogyEdgesRepository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateProductionConfirmationRequest, ProductionConfirmationResponse>
{
    public async Task<ProductionConfirmationResponse> Handle(CreateProductionConfirmationRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var order = await ordersRepository.GetAsync(request.ProductionOrderId, cancellationToken)
            ?? throw new NotFoundException("ProductionOrder", request.ProductionOrderId);

        if (order.Status is ProductionOrderStatus.Completed or ProductionOrderStatus.Closed)
            throw new ConflictException("Confirmations cannot be reported against Completed or Closed orders.");

        if (order.Status is not (ProductionOrderStatus.Released or ProductionOrderStatus.InProgress))
            throw new ValidationException(nameof(request.ProductionOrderId), "Confirmations can only be reported against Released or InProgress orders.");

        if (request.MachineId == Guid.Empty)
            throw new ValidationException(nameof(request.MachineId), "Machine is required.");

        if (request.GoodQuantity < 0)
            throw new ValidationException(nameof(request.GoodQuantity), "Good quantity cannot be negative.");

        if (request.ScrapQuantity < 0)
            throw new ValidationException(nameof(request.ScrapQuantity), "Scrap quantity cannot be negative.");

        if (request.GoodQuantity <= 0 && request.ScrapQuantity <= 0)
            throw new ValidationException(nameof(request.GoodQuantity), "At least one of Good or Scrap quantity must be greater than zero.");

        if (request.ReportedAt == default)
            throw new ValidationException(nameof(request.ReportedAt), "Reported at is required.");

        var reportedAt = request.ReportedAt.ToUniversalTime();

        if (reportedAt > dateTimeProvider.UtcNow.AddMinutes(1))
            throw new ValidationException(nameof(request.ReportedAt), "Reported at cannot be in the future.");

        if (order.ReleasedAt.HasValue && reportedAt < order.ReleasedAt.Value)
            throw new ValidationException(nameof(request.ReportedAt), "Reported at cannot be before the order was released.");

        if (request.Notes is { Length: > 1000 })
            throw new ValidationException(nameof(request.Notes), "Notes cannot exceed 1000 characters.");

        var consumedLots = request.ConsumedLots ?? Array.Empty<ConsumedLotEntry>();

        if (consumedLots.Count > 0 && !request.ProducedLotId.HasValue)
            throw new ValidationException(nameof(request.ProducedLotId), "Produced lot is required when consumed lots are provided.");

        foreach (var entry in consumedLots)
        {
            if (entry.LotId == Guid.Empty)
                throw new ValidationException(nameof(request.ConsumedLots), "Consumed lot id is required.");

            if (entry.Quantity <= 0)
                throw new ValidationException(nameof(request.ConsumedLots), "Consumed quantity must be greater than zero.");

            if (request.ProducedLotId.HasValue && entry.LotId == request.ProducedLotId.Value)
                throw new ValidationException(nameof(request.ProducedLotId), "Consumed and produced lots must differ.");
        }

        // Resolve lots before persisting so unknown or cross-tenant ids fail
        // fast with 404 and leave no orphan confirmation behind. Cross-tenant
        // rows are hidden by the global query filter, so GetAsync returns null.
        Lot? producedLot = null;
        if (request.ProducedLotId.HasValue)
            producedLot = await lotsRepository.GetAsync(request.ProducedLotId.Value, cancellationToken)
                ?? throw new NotFoundException("Lot", request.ProducedLotId.Value);

        var consumedResolved = new List<(ConsumedLotEntry Entry, Lot Lot)>();
        foreach (var entry in consumedLots)
        {
            var lot = await lotsRepository.GetAsync(entry.LotId, cancellationToken)
                ?? throw new NotFoundException("Lot", entry.LotId);
            consumedResolved.Add((entry, lot));
        }

        // Post the RW/PW ledger lines using the same BOM inputs as the
        // movement preview, so persisted lines match the preview lines for
        // this confirmation (PW for the good quantity plus RW per BOM item).
        var bomItems = await childEntitiesRepository.ListBomItemsForVersionAsync(
            order.RecipeVersionId, cancellationToken);
        var preview = MovementCalculator.BuildPreview(order, request.GoodQuantity, 1, bomItems);
        var confirmationId = guidProvider.NewGuid();
        var now = dateTimeProvider.UtcNow;
        var confirmation = new ProductionConfirmation
        {
            Id = confirmationId,
            TenantId = tenantContext.TenantId,
            ProductionOrderId = order.Id,
            MachineId = request.MachineId,
            ReportedByOperatorId = request.ReportedByOperatorId,
            ReportedAt = reportedAt,
            GoodQuantity = request.GoodQuantity,
            ScrapQuantity = request.ScrapQuantity,
            Notes = request.Notes,
            CreatedAt = now
        };

        var movements = preview.Select(line => new StockMovement
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            MovementType = line.MovementType,
            ProductId = line.ProductId,
            Quantity = line.Quantity,
            MeasureUnitId = line.MeasureUnitId,
            WarehouseId = line.PreferredWarehouseId,
            ProductionConfirmationId = confirmationId,
            ProductionOrderId = order.Id,
            ReportedAt = reportedAt,
            CreatedAt = now
        }).ToList();

        var edges = consumedResolved.Select(pair => new LotGenealogyEdge
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            ConsumedLotId = pair.Entry.LotId,
            ProducedLotId = producedLot!.Id,
            ProductionOrderId = order.Id,
            ProductionConfirmationId = confirmationId,
            MachineId = request.MachineId,
            ReportedByOperatorId = request.ReportedByOperatorId,
            ConsumedQuantity = pair.Entry.Quantity,
            OccurredAt = reportedAt,
            CreatedAt = now
        }).ToList();

        var flipToInProgress = order.Status == ProductionOrderStatus.Released;

        // All four writes share one database transaction: the confirmation
        // row, the RW/PW movements, the genealogy edges, and the order status
        // flip commit together, and any mid-fan-out failure rolls back the
        // whole batch so no orphan confirmation survives. Validation above
        // runs before any write, so 404/400/409 paths still write nothing.
        return await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await confirmationsRepository.AddAsync(confirmation, cancellationToken);

            await stockMovementsRepository.AddRangeAsync(movements, cancellationToken);

            // Post one genealogy edge per consumed lot entry so every confirmed
            // production run leaves an auditable trace without a second manual
            // call. Edges carry the same order, confirmation, Work Center,
            // Operator and ReportedAt timestamp as the confirmation.
            foreach (var edge in edges)
            {
                await genealogyEdgesRepository.AddAsync(edge, cancellationToken);
            }

            if (flipToInProgress)
            {
                order.Status = ProductionOrderStatus.InProgress;
                await ordersRepository.UpdateAsync(order, cancellationToken);
            }

            // Throughput + latency, success path only: rejected confirmations
            // are not production output. Recorded inside the request scope so
            // the exporter links each sample to the active trace as an exemplar.
            var response = BrowseProductionConfirmationsRequestHandler.Map(confirmation);
            MesMeters.RecordConfirmation(request.MachineId, tenantContext.TenantId, stopwatch.Elapsed);
            return response;
        }, cancellationToken);
    }
}
