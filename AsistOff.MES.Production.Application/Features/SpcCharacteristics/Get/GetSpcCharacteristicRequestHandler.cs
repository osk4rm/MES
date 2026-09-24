using AsistOff.MES.Production.Application.Features.SpcCharacteristics.Browse;
using AsistOff.MES.Production.Application.Features.SpcCharacteristics.Responses;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.SpcCharacteristics.Get;

internal sealed class GetSpcCharacteristicRequestHandler(ISpcCharacteristicsRepository repository)
    : IRequestHandler<GetSpcCharacteristicRequest, SpcCharacteristicResponse>
{
    public async Task<SpcCharacteristicResponse> Handle(
        GetSpcCharacteristicRequest request, CancellationToken cancellationToken)
    {
        var characteristic = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("SpcCharacteristic", request.Id);

        return BrowseSpcCharacteristicsRequestHandler.Map(characteristic);
    }
}
