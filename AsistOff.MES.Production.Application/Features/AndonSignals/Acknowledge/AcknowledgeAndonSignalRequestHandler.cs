using AsistOff.MES.Production.Application.Features.AndonSignals.Browse;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.AndonSignals.Acknowledge;

internal sealed class AcknowledgeAndonSignalRequestHandler(
    IAndonSignalsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AcknowledgeAndonSignalRequest, AndonSignalResponse>
{
    public async Task<AndonSignalResponse> Handle(AcknowledgeAndonSignalRequest request, CancellationToken cancellationToken)
    {
        var signal = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("AndonSignal", request.Id);

        if (signal.Status != AndonSignalStatus.Active)
            throw new ConflictException($"Only an Active Andon signal can be acknowledged (current status: {signal.Status}).");

        signal.Status = AndonSignalStatus.Acknowledged;
        signal.AcknowledgedAt = dateTimeProvider.UtcNow;
        signal.UpdatedAt = dateTimeProvider.UtcNow;

        await repository.UpdateAsync(signal, cancellationToken);
        return BrowseAndonSignalsRequestHandler.Map(signal);
    }
}
