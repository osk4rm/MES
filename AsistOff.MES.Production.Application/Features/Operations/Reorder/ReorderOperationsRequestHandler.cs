using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Repositories;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Operations.Reorder;

internal sealed class ReorderOperationsRequestHandler(
    IOperationNodesRepository operationsRepository,
    IRecipeVersionsRepository versionsRepository)
    : IRequestHandler<ReorderOperationsRequest>
{
    public async Task Handle(ReorderOperationsRequest request, CancellationToken cancellationToken)
    {
        await VersionGuard.EnsureDraftAsync(versionsRepository, request.VersionId, cancellationToken);

        var ops = await operationsRepository.ListForVersionAsync(request.VersionId, cancellationToken);
        var byId = ops.ToDictionary(o => o.Id);

        foreach (var entry in request.Order)
        {
            if (byId.TryGetValue(entry.OperationId, out var op))
                op.SortIndex = entry.SortIndex;
        }

        await operationsRepository.SaveChangesAsync(cancellationToken);
    }
}
