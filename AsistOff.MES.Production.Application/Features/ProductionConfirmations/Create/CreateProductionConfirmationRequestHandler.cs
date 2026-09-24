using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionConfirmations.Create;

internal sealed class CreateProductionConfirmationRequestHandler(
    IProductionConfirmationsRepository confirmationsRepository,
    IProductionOrdersRepository ordersRepository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateProductionConfirmationRequest, ProductionConfirmationResponse>
{
    public async Task<ProductionConfirmationResponse> Handle(CreateProductionConfirmationRequest request, CancellationToken cancellationToken)
    {
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

        var confirmation = new ProductionConfirmation
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            ProductionOrderId = order.Id,
            MachineId = request.MachineId,
            ReportedByOperatorId = request.ReportedByOperatorId,
            ReportedAt = reportedAt,
            GoodQuantity = request.GoodQuantity,
            ScrapQuantity = request.ScrapQuantity,
            Notes = request.Notes,
            CreatedAt = dateTimeProvider.UtcNow
        };

        await confirmationsRepository.AddAsync(confirmation, cancellationToken);

        if (order.Status == ProductionOrderStatus.Released)
        {
            order.Status = ProductionOrderStatus.InProgress;
            await ordersRepository.UpdateAsync(order, cancellationToken);
        }

        return BrowseProductionConfirmationsRequestHandler.Map(confirmation);
    }
}
