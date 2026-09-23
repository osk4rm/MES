using AsistOff.MES.Configuration.Application.Features.WorkCenterCalendar.Responses;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.WorkCenterCalendar.Get;

internal sealed class GetWorkCenterCalendarRequestHandler(
    IMachinesRepository machinesRepository,
    IWorkCenterCalendarsRepository calendarsRepository)
    : IRequestHandler<GetWorkCenterCalendarRequest, WorkCenterCalendarResponse>
{
    public async Task<WorkCenterCalendarResponse> Handle(GetWorkCenterCalendarRequest request, CancellationToken cancellationToken)
    {
        _ = await machinesRepository.GetByIdAsync(request.MachineId, cancellationToken)
            ?? throw new NotFoundException("Machine", request.MachineId);

        var calendar = await calendarsRepository.GetByMachineIdAsync(request.MachineId, cancellationToken);

        if (calendar is null)
            return new WorkCenterCalendarResponse(Guid.Empty, request.MachineId, []);

        return Save.SaveWorkCenterCalendarRequestHandler.Map(calendar);
    }
}
