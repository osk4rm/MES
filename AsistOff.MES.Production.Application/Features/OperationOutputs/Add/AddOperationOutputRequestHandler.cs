using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OperationOutputs.Add;

internal sealed class AddOperationOutputRequestHandler(
    IChildEntitiesRepository childRepository,
    IOperationNodesRepository operationsRepository,
    IRecipeVersionsRepository versionsRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<AddOperationOutputRequest, Guid>
{
    public async Task<Guid> Handle(AddOperationOutputRequest request, CancellationToken cancellationToken)
    {
        var op = await operationsRepository.GetWithDetailsAsync(request.OperationId, cancellationToken)
            ?? throw new NotFoundException("OperationNode", request.OperationId);

        await VersionGuard.EnsureDraftAsync(versionsRepository, op.RecipeVersionId, cancellationToken);

        var sortIndex = request.SortIndex ??
            ((op.Outputs.Select(x => (int?)x.SortIndex).Max() ?? -1) + 1);

        var entity = new OperationOutput
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            OperationNodeId = op.Id,
            ProductId = request.ProductId,
            MeasureUnitId = request.MeasureUnitId,
            Quantity = request.Quantity,
            QuantityType = request.QuantityType,
            OutputType = request.OutputType,
            PreferredWarehouseId = request.PreferredWarehouseId,
            Notes = request.Notes,
            SortIndex = sortIndex
        };

        await childRepository.AddOperationOutputAsync(entity, cancellationToken);
        return entity.Id;
    }
}
