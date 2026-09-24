using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.DowntimeEvents.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.DowntimeEvents.Start;

internal sealed class StartDowntimeEventRequestHandler(
    IDowntimeEventsRepository repository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<StartDowntimeEventRequest, DowntimeEventResponse>
{
    public async Task<DowntimeEventResponse> Handle(StartDowntimeEventRequest request, CancellationToken cancellationToken)
    {
        if (request.MachineId == Guid.Empty)
            throw new ValidationException(nameof(request.MachineId), "Machine is required.");
        if (request.ReasonCodeId == Guid.Empty)
            throw new ValidationException(nameof(request.ReasonCodeId), "Reason code is required.");
        if (request.StartedAt == default)
            throw new ValidationException(nameof(request.StartedAt), "Start time is required.");
        if (request.StartedAt > dateTimeProvider.UtcNow)
            throw new ValidationException(nameof(request.StartedAt), "Start time cannot be in the future.");

        if (await repository.HasOpenEventAsync(request.MachineId, null, cancellationToken))
            throw new ConflictException("The Work Center already has an open downtime event.");

        var now = dateTimeProvider.UtcNow;
        var entity = new DowntimeEvent
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            MachineId = request.MachineId,
            ReasonCodeId = request.ReasonCodeId,
            StartedAt = request.StartedAt,
            EndedAt = null,
            Notes = request.Notes,
            ReportedByOperatorId = request.ReportedByOperatorId,
            ProductionOrderId = null,
            CreatedAt = now
        };

        await repository.AddAsync(entity, cancellationToken);
        return BrowseDowntimeEventsRequestHandler.Map(entity);
    }
}
