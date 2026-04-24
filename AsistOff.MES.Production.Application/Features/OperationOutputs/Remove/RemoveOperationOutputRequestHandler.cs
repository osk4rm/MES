using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OperationOutputs.Remove;

internal sealed class RemoveOperationOutputRequestHandler(
    IChildEntitiesRepository childRepository,
    IOperationNodesRepository operationsRepository,
    IRecipeVersionsRepository versionsRepository)
    : IRequestHandler<RemoveOperationOutputRequest>
{
    public async Task Handle(RemoveOperationOutputRequest request, CancellationToken cancellationToken)
    {
        var entity = await childRepository.GetOperationOutputAsync(request.OutputId, cancellationToken)
            ?? throw new NotFoundException("OperationOutput", request.OutputId);

        var op = await operationsRepository.GetAsync(entity.OperationNodeId, cancellationToken)
            ?? throw new NotFoundException("OperationNode", entity.OperationNodeId);

        await VersionGuard.EnsureDraftAsync(versionsRepository, op.RecipeVersionId, cancellationToken);
        await childRepository.RemoveOperationOutputAsync(entity.Id, cancellationToken);
    }
}
