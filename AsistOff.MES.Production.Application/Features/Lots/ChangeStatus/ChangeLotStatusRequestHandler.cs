using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Lots.ChangeStatus;

internal sealed class ChangeLotStatusRequestHandler(
    ILotsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ChangeLotStatusRequest>
{
    public async Task Handle(ChangeLotStatusRequest request, CancellationToken cancellationToken)
    {
        var lot = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Lot", request.Id);

        if (!LotMappings.IsTransitionAllowed(lot.Status, request.Status))
            throw new ValidationException(nameof(request.Status),
                $"Status transition from '{lot.Status}' to '{request.Status}' is not allowed.");

        lot.Status = request.Status;
        lot.UpdatedAt = dateTimeProvider.UtcNow;

        await repository.UpdateAsync(lot, cancellationToken);
    }
}
