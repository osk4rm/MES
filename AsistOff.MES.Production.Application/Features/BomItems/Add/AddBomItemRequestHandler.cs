using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.BomItems.Add;

internal sealed class AddBomItemRequestHandler(
    IChildEntitiesRepository childRepository,
    IOperationNodesRepository operationsRepository,
    IRecipeVersionsRepository versionsRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<AddBomItemRequest, Guid>
{
    public async Task<Guid> Handle(AddBomItemRequest request, CancellationToken cancellationToken)
    {
        // Lightweight load — we only need OperationNodeId / RecipeVersionId / TenantId here.
        // We deliberately avoid GetWithDetailsAsync (which eagerly loads BomItems / Outputs /
        // ResourceRequirements / Dependencies via split queries). Mutating the navigation
        // collection of a tracked principal under that loading pattern was producing a
        // DbUpdateConcurrencyException on save; using DbSet.Add directly mirrors the proven
        // pattern already used by AddOperationOutput / AddResourceRequirement.
        var op = await operationsRepository.GetAsync(request.OperationId, cancellationToken)
            ?? throw new NotFoundException("OperationNode", request.OperationId);

        await VersionGuard.EnsureDraftAsync(versionsRepository, op.RecipeVersionId, cancellationToken);

        var sortIndex = request.SortIndex ??
            ((await childRepository.GetMaxBomItemSortIndexAsync(op.Id, cancellationToken) ?? -1) + 1);

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

        await childRepository.AddBomItemAsync(entity, cancellationToken);
        return entity.Id;
    }
}
