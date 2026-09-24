using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Lots.Delete;

internal sealed class DeleteLotRequestHandler(ILotsRepository repository)
    : IRequestHandler<DeleteLotRequest>
{
    public async Task Handle(DeleteLotRequest request, CancellationToken cancellationToken)
    {
        var lot = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Lot", request.Id);

        if (lot.Status != LotStatus.Available)
            throw new ValidationException(nameof(lot.Status), "Only lots with status 'Available' can be deleted.");

        await repository.DeleteAsync(lot.Id, cancellationToken);
    }
}
