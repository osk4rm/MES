using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Update;

internal sealed class UpdateProductionOrderRequestHandler(IProductionOrdersRepository repository)
    : IRequestHandler<UpdateProductionOrderRequest, Common.ProductionOrderResponse>
{
    public async Task<Common.ProductionOrderResponse> Handle(UpdateProductionOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ProductionOrder", request.Id);

        ProductionOrderConcurrency.RequireMatchForUpdate(order, request.ConcurrencyToken);

        if (order.Status != ProductionOrderStatus.Planned)
            throw new ConflictException("Only orders in Planned status can be edited.");

        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ValidationException(nameof(request.Code), "Code is required.");
        if (request.PlannedQuantity <= 0)
            throw new ValidationException(nameof(request.PlannedQuantity), "Planned quantity must be greater than zero.");

        if (!string.Equals(order.Code, request.Code, StringComparison.Ordinal) &&
            await repository.CodeExistsAsync(request.Code, request.Id, cancellationToken))
        {
            throw new ConflictException($"Production order with code '{request.Code}' already exists.");
        }

        order.Code = request.Code;
        order.ProductId = request.ProductId;
        order.RecipeId = request.RecipeId;
        order.RecipeVersionId = request.RecipeVersionId;
        order.PlannedQuantity = request.PlannedQuantity;
        order.MeasureUnitId = request.MeasureUnitId;
        order.Priority = request.Priority;
        order.DueDate = request.DueDate;
        order.Notes = request.Notes;
        order.SyncId = request.SyncId;

        await repository.UpdateAsync(order, cancellationToken);

        return ProductionOrderMappers.Map(order);
    }
}
