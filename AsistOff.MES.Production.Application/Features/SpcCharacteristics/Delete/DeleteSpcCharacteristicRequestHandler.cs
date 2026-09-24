using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.SpcCharacteristics.Delete;

internal sealed class DeleteSpcCharacteristicRequestHandler(ISpcCharacteristicsRepository repository)
    : IRequestHandler<DeleteSpcCharacteristicRequest>
{
    public async Task Handle(DeleteSpcCharacteristicRequest request, CancellationToken cancellationToken)
    {
        var characteristic = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("SpcCharacteristic", request.Id);

        await repository.DeleteAsync(characteristic.Id, cancellationToken);
    }
}
