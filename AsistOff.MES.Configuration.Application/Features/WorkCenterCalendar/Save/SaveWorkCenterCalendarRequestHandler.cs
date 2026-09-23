using AsistOff.MES.Configuration.Application.Features.WorkCenterCalendar.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.WorkCenterCalendar.Save;

internal sealed class SaveWorkCenterCalendarRequestHandler(
    IMachinesRepository machinesRepository,
    IShiftsRepository shiftsRepository,
    IWorkCenterCalendarsRepository calendarsRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<SaveWorkCenterCalendarRequest, WorkCenterCalendarResponse>
{
    public async Task<WorkCenterCalendarResponse> Handle(SaveWorkCenterCalendarRequest request, CancellationToken cancellationToken)
    {
        _ = await machinesRepository.GetByIdAsync(request.MachineId, cancellationToken)
            ?? throw new NotFoundException("Machine", request.MachineId);

        foreach (var entry in request.Entries)
        {
            if (!Enum.IsDefined(entry.DayOfWeek))
                throw new ValidationException(nameof(entry.DayOfWeek), $"Invalid day of week value '{(int)entry.DayOfWeek}'.");
        }

        WorkCenterCalendarOverlapValidator.ThrowIfOverlapping(request.Entries);

        var shiftIds = request.Entries
            .Where(x => x.ShiftId.HasValue)
            .Select(x => x.ShiftId!.Value)
            .Distinct()
            .ToList();
        foreach (var shiftId in shiftIds)
        {
            // Runs through the tenant query filter, so a cross-tenant ShiftId
            // is reported as unknown rather than leaked.
            _ = await shiftsRepository.GetByIdAsync(shiftId, cancellationToken)
                ?? throw new ValidationException(nameof(WorkCenterCalendarEntryInput.ShiftId), $"Shift '{shiftId}' was not found.");
        }

        var calendar = await calendarsRepository.GetByMachineIdAsync(request.MachineId, cancellationToken);
        if (calendar is null)
        {
            calendar = new Domain.Entities.WorkCenterCalendar
            {
                Id = guidProvider.NewGuid(),
                TenantId = tenantContext.TenantId,
                MachineId = request.MachineId
            };
            await calendarsRepository.AddAsync(calendar, cancellationToken);
        }

        var entries = request.Entries.Select(input => new WorkCenterCalendarEntry
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            CalendarId = calendar.Id,
            DayOfWeek = input.DayOfWeek,
            StartTime = input.StartTime,
            EndTime = input.EndTime,
            ShiftId = input.ShiftId,
            IsWorking = input.IsWorking
        }).ToList();

        await calendarsRepository.ReplaceEntriesAsync(calendar.Id, entries, cancellationToken);

        var saved = await calendarsRepository.GetByMachineIdAsync(request.MachineId, cancellationToken);
        return Map(saved ?? calendar);
    }

    internal static WorkCenterCalendarResponse Map(Domain.Entities.WorkCenterCalendar calendar) => new(
        calendar.Id,
        calendar.MachineId,
        calendar.Entries
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.StartTime)
            .Select(x => new WorkCenterCalendarEntryResponse(
                x.Id, x.DayOfWeek, x.StartTime, x.EndTime,
                x.ShiftId, x.Shift?.Code, x.Shift?.Name, x.IsWorking))
            .ToList());
}
