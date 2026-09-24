using AsistOff.MES.Production.Application.Features.AndonSignals.Browse;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.AndonSignals.Resolve;

internal sealed class ResolveAndonSignalRequestHandler(
    IAndonSignalsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ResolveAndonSignalRequest, AndonSignalResponse>
{
    public async Task<AndonSignalResponse> Handle(ResolveAndonSignalRequest request, CancellationToken cancellationToken)
    {
        var signal = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("AndonSignal", request.Id);

        if (signal.Status == AndonSignalStatus.Resolved)
            throw new ConflictException("An already Resolved Andon signal cannot be resolved again.");

        var resolvedAt = request.ResolvedAt ?? dateTimeProvider.UtcNow;

        if (resolvedAt < signal.RaisedAt)
            throw new ValidationException(nameof(request.ResolvedAt), "ResolvedAt must be greater than or equal to RaisedAt.");

        signal.Status = AndonSignalStatus.Resolved;
        signal.ResolvedAt = resolvedAt;
        signal.UpdatedAt = dateTimeProvider.UtcNow;

        await repository.UpdateAsync(signal, cancellationToken);
        return BrowseAndonSignalsRequestHandler.Map(signal);
    }
}
