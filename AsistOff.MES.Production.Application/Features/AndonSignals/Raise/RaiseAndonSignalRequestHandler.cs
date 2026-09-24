using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.AndonSignals.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.AndonSignals.Raise;

internal sealed class RaiseAndonSignalRequestHandler(
    IAndonSignalsRepository repository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<RaiseAndonSignalRequest, AndonSignalResponse>
{
    public async Task<AndonSignalResponse> Handle(RaiseAndonSignalRequest request, CancellationToken cancellationToken)
    {
        if (request.ProductionOrderId.HasValue)
            throw new ValidationException(nameof(request.ProductionOrderId), "Linking a signal to a production order is not supported in this increment.");

        if (!Enum.IsDefined(request.Category))
            throw new ValidationException(nameof(request.Category), "Category is required.");

        if (request.RaisedAt == default)
            throw new ValidationException(nameof(request.RaisedAt), "RaisedAt is required.");

        if (request.RaisedAt > dateTimeProvider.UtcNow)
            throw new ValidationException(nameof(request.RaisedAt), "RaisedAt must not be in the future.");

        if (await repository.HasActiveSignalAsync(request.MachineId, null, cancellationToken))
            throw new ConflictException($"An active Andon signal already exists for work center '{request.MachineId}'.");

        var signal = new AndonSignal
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            MachineId = request.MachineId,
            Category = request.Category,
            ReasonCodeId = request.ReasonCodeId,
            Status = AndonSignalStatus.Active,
            RaisedAt = request.RaisedAt,
            Notes = request.Notes,
            RaisedByOperatorId = request.RaisedByOperatorId,
            ProductionOrderId = null,
            CreatedAt = dateTimeProvider.UtcNow
        };

        await repository.AddAsync(signal, cancellationToken);
        return BrowseAndonSignalsRequestHandler.Map(signal);
    }
}
