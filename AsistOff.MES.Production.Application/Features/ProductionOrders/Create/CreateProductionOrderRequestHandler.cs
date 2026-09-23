using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Create;

internal sealed class CreateProductionOrderRequestHandler(
    IProductionOrdersRepository repository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateProductionOrderRequest, ProductionOrderResponse>
{
    public async Task<ProductionOrderResponse> Handle(CreateProductionOrderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ValidationException(nameof(request.Code), "Code is required.");
        if (request.ProductId == Guid.Empty)
            throw new ValidationException(nameof(request.ProductId), "Product is required.");
        if (request.RecipeId == Guid.Empty)
            throw new ValidationException(nameof(request.RecipeId), "Recipe is required.");
        if (request.RecipeVersionId == Guid.Empty)
            throw new ValidationException(nameof(request.RecipeVersionId), "Recipe version is required.");
        if (request.PlannedQuantity <= 0)
            throw new ValidationException(nameof(request.PlannedQuantity), "Planned quantity must be greater than zero.");

        if (await repository.CodeExistsAsync(request.Code, null, cancellationToken))
            throw new ConflictException($"Production order with code '{request.Code}' already exists.");

        var order = new ProductionOrder
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            Code = request.Code,
            ProductId = request.ProductId,
            RecipeId = request.RecipeId,
            RecipeVersionId = request.RecipeVersionId,
            PlannedQuantity = request.PlannedQuantity,
            MeasureUnitId = request.MeasureUnitId,
            Priority = request.Priority,
            DueDate = request.DueDate,
            Status = ProductionOrderStatus.Planned,
            Notes = request.Notes,
            SyncId = request.SyncId
        };

        await repository.AddAsync(order, cancellationToken);

        return ProductionOrderMappers.Map(order);
    }
}
