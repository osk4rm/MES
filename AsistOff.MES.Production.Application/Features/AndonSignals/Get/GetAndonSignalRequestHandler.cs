using AsistOff.MES.Production.Application.Features.AndonSignals.Browse;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.AndonSignals.Get;

internal sealed class GetAndonSignalRequestHandler(IAndonSignalsRepository repository)
    : IRequestHandler<GetAndonSignalRequest, AndonSignalResponse>
{
    public async Task<AndonSignalResponse> Handle(GetAndonSignalRequest request, CancellationToken cancellationToken)
    {
        var signal = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("AndonSignal", request.Id);

        return BrowseAndonSignalsRequestHandler.Map(signal);
    }
}
