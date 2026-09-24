using AsistOff.MES.Configuration.Application.Features.Shifts.Browse;
using AsistOff.MES.Configuration.Application.Features.Shifts.Responses;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Shifts.Get;

internal sealed class GetShiftRequestHandler(IShiftsRepository repository)
    : IRequestHandler<GetShiftRequest, ShiftResponse>
{
    public async Task<ShiftResponse> Handle(GetShiftRequest request, CancellationToken cancellationToken)
    {
        var shift = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Shift", request.Id);

        return BrowseShiftsRequestHandler.Map(shift);
    }
}
