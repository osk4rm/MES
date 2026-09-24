using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.LotGenealogy.Delete;

/// <summary>
/// Correction-only delete for the append-only genealogy log: there is no
/// update path, corrections are delete plus re-record.
/// </summary>
internal sealed class DeleteLotGenealogyEdgeRequestHandler(ILotGenealogyEdgesRepository repository)
    : IRequestHandler<DeleteLotGenealogyEdgeRequest>
{
    public async Task Handle(DeleteLotGenealogyEdgeRequest request, CancellationToken cancellationToken)
    {
        var edge = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("LotGenealogyEdge", request.Id);

        await repository.DeleteAsync(edge.Id, cancellationToken);
    }
}
