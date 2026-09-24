using AsistOff.MES.Configuration.Application.Features.WorkCenterCalendars.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.WorkCenterCalendars.Get;

internal sealed class GetWorkCenterCalendarRequestHandler(
    IMachinesRepository machinesRepository,
    IWorkCenterCalendarsRepository calendarsRepository)
    : IRequestHandler<GetWorkCenterCalendarRequest, WorkCenterCalendarResponse>
{
    public async Task<WorkCenterCalendarResponse> Handle(
        GetWorkCenterCalendarRequest request, CancellationToken cancellationToken)
    {
        var machine = await machinesRepository.GetByIdAsync(request.MachineId, cancellationToken)
            ?? throw new NotFoundException("Machine", request.MachineId);

        var calendar = await calendarsRepository.GetByMachineIdAsync(request.MachineId, cancellationToken);

        return WorkCenterCalendarMapper.Map(calendar, machine);
    }
}

internal static class WorkCenterCalendarMapper
{
    /// <summary>
    /// Maps a calendar (which may not exist yet) together with its Work Center
    /// into the API contract. A missing calendar is represented by
    /// <see cref="Guid.Empty"/> and an empty entry collection so the client can
    /// render an editor without a 404 round-trip.
    /// </summary>
    internal static WorkCenterCalendarResponse Map(WorkCenterCalendar? calendar, Machine machine) =>
        new(
            calendar?.Id ?? Guid.Empty,
            machine.Id,
            machine.Code,
            machine.Name,
            calendar is null
                ? []
                : calendar.Entries
                    .OrderBy(e => (int)e.DayOfWeek)
                    .ThenBy(e => e.StartTime)
                    .Select(MapEntry)
                    .ToList());

    internal static WorkCenterCalendarEntryResponse MapEntry(WorkCenterCalendarEntry entry) =>
        new(entry.Id, entry.DayOfWeek, entry.StartTime, entry.EndTime, entry.ShiftId, entry.IsWorking);
}
