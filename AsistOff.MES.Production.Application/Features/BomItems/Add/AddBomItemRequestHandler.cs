using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.BomItems.Add;

internal sealed class AddBomItemRequestHandler(
    IOperationNodesRepository operationsRepository,
    IRecipeVersionsRepository versionsRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<AddBomItemRequest, Guid>
{
    public async Task<Guid> Handle(AddBomItemRequest request, CancellationToken cancellationToken)
    {
        var op = await operationsRepository.GetWithDetailsAsync(request.OperationId, cancellationToken)
            ?? throw new NotFoundException("OperationNode", request.OperationId);

        await VersionGuard.EnsureDraftAsync(versionsRepository, op.RecipeVersionId, cancellationToken);

        var sortIndex = request.SortIndex ??
            ((op.BomItems.Select(b => (int?)b.SortIndex).Max() ?? -1) + 1);

        var entity = new BomItem
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            OperationNodeId = op.Id,
            ProductId = request.ProductId,
            MeasureUnitId = request.MeasureUnitId,
            Quantity = request.Quantity,
            QuantityType = request.QuantityType,
            ScrapPercentage = request.ScrapPercentage,
            IsOptional = request.IsOptional,
            PreferredWarehouseId = request.PreferredWarehouseId,
            ConsumptionTiming = request.ConsumptionTiming,
            Notes = request.Notes,
            SortIndex = sortIndex
        };

        op.BomItems.Add(entity);
        await operationsRepository.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}
