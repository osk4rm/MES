using AsistOff.MES.Production.Application.Features.LotGenealogy.Browse;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.LotGenealogy.Get;

internal sealed class GetLotGenealogyEdgeRequestHandler(ILotGenealogyEdgesRepository repository)
    : IRequestHandler<GetLotGenealogyEdgeRequest, LotGenealogyEdgeResponse>
{
    public async Task<LotGenealogyEdgeResponse> Handle(GetLotGenealogyEdgeRequest request, CancellationToken cancellationToken)
    {
        var edge = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("LotGenealogyEdge", request.Id);

        return BrowseLotGenealogyEdgesRequestHandler.Map(edge);
    }
}
