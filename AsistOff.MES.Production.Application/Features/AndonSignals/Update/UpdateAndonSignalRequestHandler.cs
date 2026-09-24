using AsistOff.MES.Production.Application.Features.AndonSignals.Browse;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.AndonSignals.Update;

internal sealed class UpdateAndonSignalRequestHandler(
    IAndonSignalsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateAndonSignalRequest, AndonSignalResponse>
{
    public async Task<AndonSignalResponse> Handle(UpdateAndonSignalRequest request, CancellationToken cancellationToken)
    {
        var signal = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("AndonSignal", request.Id);

        if (signal.Status == AndonSignalStatus.Resolved)
            throw new ConflictException("A Resolved Andon signal is immutable and cannot be edited.");

        if (!Enum.IsDefined(request.Category))
            throw new ValidationException(nameof(request.Category), "Category is required.");

        signal.Category = request.Category;
        signal.ReasonCodeId = request.ReasonCodeId;
        signal.Notes = request.Notes;
        signal.UpdatedAt = dateTimeProvider.UtcNow;

        await repository.UpdateAsync(signal, cancellationToken);
        return BrowseAndonSignalsRequestHandler.Map(signal);
    }
}
