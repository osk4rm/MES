using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.AndonSignals.Delete;

internal sealed class DeleteAndonSignalRequestHandler(IAndonSignalsRepository repository)
    : IRequestHandler<DeleteAndonSignalRequest>
{
    public async Task Handle(DeleteAndonSignalRequest request, CancellationToken cancellationToken)
    {
        var signal = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("AndonSignal", request.Id);

        if (signal.Status == AndonSignalStatus.Resolved)
            throw new ConflictException("A Resolved Andon signal cannot be deleted.");

        await repository.DeleteAsync(signal.Id, cancellationToken);
    }
}
