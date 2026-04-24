using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.RecipeVersions.Clone;

internal sealed class CloneRecipeVersionRequestHandler(
    IRecipeVersionsRepository versionsRepository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CloneRecipeVersionRequest, RecipeVersionDetailResponse>
{
    public async Task<RecipeVersionDetailResponse> Handle(CloneRecipeVersionRequest request, CancellationToken cancellationToken)
    {
        var source = await versionsRepository.GetFullAsync(request.SourceVersionId, cancellationToken)
            ?? throw new NotFoundException("RecipeVersion", request.SourceVersionId);

        var nextNumber = await versionsRepository.GetNextVersionNumberAsync(source.RecipeId, cancellationToken);

        var target = new RecipeVersion
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            RecipeId = source.RecipeId,
            VersionNumber = nextNumber,
            Status = RecipeVersionStatus.Draft,
            ChangeNotes = request.ChangeNotes,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            CreatedAt = dateTimeProvider.UtcNow
        };

        // Map old operation IDs to newly-generated ones so dependency edges
        // can be rewired to the cloned nodes.
        var idMap = new Dictionary<Guid, Guid>();
        foreach (var op in source.Operations)
            idMap[op.Id] = guidProvider.NewGuid();

        foreach (var op in source.Operations)
        {
            var newOp = new OperationNode
            {
                Id = idMap[op.Id],
                TenantId = tenantContext.TenantId,
                RecipeVersionId = target.Id,
                Code = op.Code,
                Name = op.Name,
                Description = op.Description,
                OperationType = op.OperationType,
                SortIndex = op.SortIndex,
                SetupTimeMinutes = op.SetupTimeMinutes,
                RunTimeMode = op.RunTimeMode,
                RunTimePerUnitSeconds = op.RunTimePerUnitSeconds,
                RunTimePerBatchMinutes = op.RunTimePerBatchMinutes,
                TeardownTimeMinutes = op.TeardownTimeMinutes,
                QueueTimeMinutes = op.QueueTimeMinutes,
                IsOptional = op.IsOptional,
                AllowParallelExecution = op.AllowParallelExecution,
                ExpectedQuantity = op.ExpectedQuantity
            };

            foreach (var dep in op.Dependencies)
            {
                // Only keep edges whose predecessor is within the source version.
                if (!idMap.TryGetValue(dep.PredecessorOperationNodeId, out var mappedPredecessorId))
                    continue;

                newOp.Dependencies.Add(new OperationDependency
                {
                    Id = guidProvider.NewGuid(),
                    TenantId = tenantContext.TenantId,
                    RecipeVersionId = target.Id,
                    OperationNodeId = newOp.Id,
                    PredecessorOperationNodeId = mappedPredecessorId,
                    DependencyType = dep.DependencyType,
                    LagMinutes = dep.LagMinutes
                });
            }

            foreach (var bom in op.BomItems)
            {
                newOp.BomItems.Add(new BomItem
                {
                    Id = guidProvider.NewGuid(),
                    TenantId = tenantContext.TenantId,
                    OperationNodeId = newOp.Id,
                    ProductId = bom.ProductId,
                    MeasureUnitId = bom.MeasureUnitId,
                    Quantity = bom.Quantity,
                    QuantityType = bom.QuantityType,
                    ScrapPercentage = bom.ScrapPercentage,
                    IsOptional = bom.IsOptional,
                    PreferredWarehouseId = bom.PreferredWarehouseId,
                    ConsumptionTiming = bom.ConsumptionTiming,
                    Notes = bom.Notes,
                    SortIndex = bom.SortIndex
                });
            }

            foreach (var output in op.Outputs)
            {
                newOp.Outputs.Add(new OperationOutput
                {
                    Id = guidProvider.NewGuid(),
                    TenantId = tenantContext.TenantId,
                    OperationNodeId = newOp.Id,
                    ProductId = output.ProductId,
                    MeasureUnitId = output.MeasureUnitId,
                    Quantity = output.Quantity,
                    QuantityType = output.QuantityType,
                    OutputType = output.OutputType,
                    PreferredWarehouseId = output.PreferredWarehouseId,
                    Notes = output.Notes,
                    SortIndex = output.SortIndex
                });
            }

            foreach (var resource in op.ResourceRequirements)
            {
                newOp.ResourceRequirements.Add(new ResourceRequirement
                {
                    Id = guidProvider.NewGuid(),
                    TenantId = tenantContext.TenantId,
                    OperationNodeId = newOp.Id,
                    PreferredDepartmentId = resource.PreferredDepartmentId,
                    PreferredMachineId = resource.PreferredMachineId,
                    RequiredCapability = resource.RequiredCapability,
                    RequiredOperatorCount = resource.RequiredOperatorCount,
                    RequiredRole = resource.RequiredRole,
                    Notes = resource.Notes
                });
            }

            target.Operations.Add(newOp);
        }

        await versionsRepository.AddAsync(target, cancellationToken);

        var full = await versionsRepository.GetFullAsync(target.Id, cancellationToken);
        return ProductionMappers.MapDetail(full!);
    }
}
