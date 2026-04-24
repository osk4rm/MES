using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Operations.Delete;

internal sealed class DeleteOperationRequestHandler(
    IOperationNodesRepository operationsRepository,
    IRecipeVersionsRepository versionsRepository)
    : IRequestHandler<DeleteOperationRequest>
{
    public async Task Handle(DeleteOperationRequest request, CancellationToken cancellationToken)
    {
        var op = await operationsRepository.GetAsync(request.OperationId, cancellationToken)
            ?? throw new NotFoundException("OperationNode", request.OperationId);

        await VersionGuard.EnsureDraftAsync(versionsRepository, op.RecipeVersionId, cancellationToken);
        await operationsRepository.DeleteAsync(op.Id, cancellationToken);
    }
}
