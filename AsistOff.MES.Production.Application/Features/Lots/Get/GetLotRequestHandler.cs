using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Lots.Get;

internal sealed class GetLotRequestHandler(ILotsRepository repository)
    : IRequestHandler<GetLotRequest, LotResponse>
{
    public async Task<LotResponse> Handle(GetLotRequest request, CancellationToken cancellationToken)
    {
        var lot = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Lot", request.Id);

        return LotMappings.Map(lot);
    }
}
