using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Operations.Add;

internal sealed class AddOperationRequestHandler(
    IRecipeVersionsRepository versionsRepository,
    IOperationNodesRepository operationsRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<AddOperationRequest, OperationNodeResponse>
{
    public async Task<OperationNodeResponse> Handle(AddOperationRequest request, CancellationToken cancellationToken)
    {
        var version = await VersionGuard.EnsureDraftAsync(versionsRepository, request.VersionId, cancellationToken);

        int sortIndex = request.SortIndex ??
            ((await operationsRepository.ListForVersionAsync(version.Id, cancellationToken))
                .Select(o => (int?)o.SortIndex).Max() ?? -1) + 1;

        var op = new OperationNode
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            RecipeVersionId = version.Id,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            OperationType = request.OperationType,
            SortIndex = sortIndex,
            SetupTimeMinutes = request.SetupTimeMinutes,
            RunTimeMode = request.RunTimeMode,
            RunTimePerUnitSeconds = request.RunTimePerUnitSeconds,
            RunTimePerBatchMinutes = request.RunTimePerBatchMinutes,
            TeardownTimeMinutes = request.TeardownTimeMinutes,
            QueueTimeMinutes = request.QueueTimeMinutes,
            IsOptional = request.IsOptional,
            AllowParallelExecution = request.AllowParallelExecution,
            ExpectedQuantity = request.ExpectedQuantity
        };

        await operationsRepository.AddAsync(op, cancellationToken);

        var reloaded = await operationsRepository.GetWithDetailsAsync(op.Id, cancellationToken)!;
        return ProductionMappers.Map(reloaded!);
    }
}
