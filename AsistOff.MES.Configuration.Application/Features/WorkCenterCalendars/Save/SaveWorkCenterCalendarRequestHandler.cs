using AsistOff.MES.Configuration.Application.Features.WorkCenterCalendars.Get;
using AsistOff.MES.Configuration.Application.Features.WorkCenterCalendars.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.WorkCenterCalendars.Save;

internal sealed class SaveWorkCenterCalendarRequestHandler(
    IMachinesRepository machinesRepository,
    IWorkCenterCalendarsRepository calendarsRepository,
    IShiftsRepository shiftsRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<SaveWorkCenterCalendarRequest, WorkCenterCalendarResponse>
{
    public async Task<WorkCenterCalendarResponse> Handle(
        SaveWorkCenterCalendarRequest request, CancellationToken cancellationToken)
    {
        var machine = await machinesRepository.GetByIdAsync(request.MachineId, cancellationToken)
            ?? throw new NotFoundException("Machine", request.MachineId);

        var requestedEntries = request.Entries.ToList();

        EnsureValidDaysOfWeek(requestedEntries);
        CalendarEntryOverlapValidator.EnsureNoOverlaps(requestedEntries);
        await EnsureShiftsExistAsync(requestedEntries, cancellationToken);

        var tenantId = tenantContext.TenantId;
        var entries = requestedEntries.Select(e => new WorkCenterCalendarEntry
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantId,
            DayOfWeek = e.DayOfWeek,
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            ShiftId = e.ShiftId,
            IsWorking = e.IsWorking
        }).ToList();

        var existing = await calendarsRepository.GetByMachineIdAsync(machine.Id, cancellationToken);

        Guid calendarId;
        if (existing is null)
        {
            var calendar = new WorkCenterCalendar
            {
                Id = guidProvider.NewGuid(),
                TenantId = tenantId,
                MachineId = machine.Id,
                Entries = entries
            };
            await calendarsRepository.CreateAsync(calendar, cancellationToken);
            calendarId = calendar.Id;
        }
        else
        {
            calendarId = existing.Id;
            await calendarsRepository.ReplaceEntriesAsync(existing.Id, entries, cancellationToken);
        }

        return new WorkCenterCalendarResponse(
            calendarId,
            machine.Id,
            machine.Code,
            machine.Name,
            entries
                .OrderBy(e => (int)e.DayOfWeek)
                .ThenBy(e => e.StartTime)
                .Select(WorkCenterCalendarMapper.MapEntry)
                .ToList());
    }

    private static void EnsureValidDaysOfWeek(IEnumerable<WorkCenterCalendarEntryRequest> entries)
    {
        foreach (var entry in entries)
        {
            if (!Enum.IsDefined(entry.DayOfWeek))
            {
                throw new ValidationException(
                    nameof(SaveWorkCenterCalendarRequest.Entries),
                    $"'{entry.DayOfWeek}' is not a valid day of week.");
            }
        }
    }

    private async Task EnsureShiftsExistAsync(
        IReadOnlyCollection<WorkCenterCalendarEntryRequest> entries,
        CancellationToken cancellationToken)
    {
        var shiftIds = entries
            .Where(e => e.ShiftId.HasValue)
            .Select(e => e.ShiftId!.Value)
            .Distinct()
            .ToList();

        if (shiftIds.Count == 0)
        {
            return;
        }

        // The repository query runs through the tenant query filter, so a shift
        // belonging to another tenant is reported as missing rather than linked.
        var found = await shiftsRepository.GetByIdsAsync(shiftIds, cancellationToken);
        var missing = shiftIds.Except(found.Select(s => s.Id)).ToList();

        if (missing.Count > 0)
        {
            throw new ValidationException(
                nameof(SaveWorkCenterCalendarRequest.Entries),
                $"Shift(s) {string.Join(", ", missing)} do not exist.");
        }
    }
}
