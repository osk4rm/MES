using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.ShiftHandovers.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ShiftHandovers.Create;

internal sealed class CreateShiftHandoverRequestHandler(
    IShiftHandoversRepository handoversRepository,
    IMachinesRepository machinesRepository,
    IWorkCenterCalendarsRepository calendarsRepository,
    IProductionOrdersRepository ordersRepository,
    IAndonSignalsRepository andonSignalsRepository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext,
    ICurrentUserAccessor currentUserAccessor)
    : IRequestHandler<CreateShiftHandoverRequest, ShiftHandoverResponse>
{
    public async Task<ShiftHandoverResponse> Handle(CreateShiftHandoverRequest request, CancellationToken cancellationToken)
    {
        if (request.MachineId == Guid.Empty)
            throw new ValidationException(nameof(request.MachineId), "Machine is required.");

        if (request.From == default || request.To == default)
            throw new ValidationException(nameof(request.From), "Time window is required.");

        var fromUtc = request.From.ToUniversalTime();
        var toUtc = request.To.ToUniversalTime();

        if (fromUtc >= toUtc)
            throw new ValidationException(nameof(request.From), "From must be before To.");

        if ((toUtc - fromUtc).TotalHours > CreateShiftHandoverValidator.MaxWindowHours)
            throw new ValidationException(nameof(request.To),
                $"Time window cannot exceed {CreateShiftHandoverValidator.MaxWindowHours} hours.");

        if (string.IsNullOrWhiteSpace(request.Notes))
            throw new ValidationException(nameof(request.Notes), "Notes are required.");

        if (request.Notes.Length > CreateShiftHandoverValidator.MaxNotesLength)
            throw new ValidationException(nameof(request.Notes),
                $"Notes cannot exceed {CreateShiftHandoverValidator.MaxNotesLength} characters.");

        // Tenant-scoped reads: the global query filter keeps every repository
        // call below inside the caller tenant, so unknown and cross-tenant
        // machine ids both yield 404.
        _ = await machinesRepository.GetByIdAsync(request.MachineId, cancellationToken)
            ?? throw new NotFoundException("Machine", request.MachineId);

        // One entry per shift boundary: a second POST for the same
        // (MachineId, From) is a 409, never a silent overwrite.
        if (await handoversRepository.ExistsAsync(request.MachineId, fromUtc, cancellationToken))
            throw new ConflictException(
                $"A shift handover already exists for work center '{request.MachineId}' at '{fromUtc:o}'.");

        // Shift window from the Work Center calendar entry covering the window
        // start (same 1/2 context logic). No entry means shiftId null; only a
        // machine-scoped gap is flagged uncovered.
        var calendar = await calendarsRepository.GetByMachineIdAsync(request.MachineId, cancellationToken);
        var entry = calendar is null
            ? null
            : ShiftWindowResolver.FindCoveringEntry(calendar.Entries, fromUtc);
        var shiftId = entry?.ShiftId;
        var uncoveredShift = entry is null;

        // Denormalized context snapshot counts at creation time: tenant-wide
        // open orders (ProductionOrder carries no MachineId FK) plus active
        // Andon signals scoped to the machine.
        var openOrdersPredicate = PredicateBuilder.New<ProductionOrder>(true)
            .And(x => x.Status == ProductionOrderStatus.Released
                || x.Status == ProductionOrderStatus.InProgress);
        var openOrdersCount = await ordersRepository.CountAsync(openOrdersPredicate, cancellationToken);

        var activeSignalsPredicate = PredicateBuilder.New<AndonSignal>(true)
            .And(x => x.Status == AndonSignalStatus.Active)
            .And(x => x.MachineId == request.MachineId);
        var activeAndonCount = await andonSignalsRepository.CountAsync(activeSignalsPredicate, cancellationToken);

        var handover = new ShiftHandover
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            MachineId = request.MachineId,
            ShiftId = shiftId,
            From = fromUtc,
            To = toUtc,
            Notes = request.Notes,
            OpenOrdersCount = openOrdersCount,
            ActiveAndonCount = activeAndonCount,
            // Server-set audit fields: the AuditableEntityInterceptor stamps
            // the same values on save; setting them here keeps the in-memory
            // response and unit-test assertions honest.
            CreatedAt = dateTimeProvider.UtcNow,
            CreatedBy = currentUserAccessor.UserId
        };

        await handoversRepository.AddAsync(handover, cancellationToken);

        return BrowseShiftHandoversRequestHandler.Map(handover, uncoveredShift);
    }
}
