using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Shifts.Delete;

internal sealed class DeleteShiftRequestHandler(IShiftsRepository repository)
    : IRequestHandler<DeleteShiftRequest>
{
    public async Task Handle(DeleteShiftRequest request, CancellationToken cancellationToken)
    {
        var shift = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Shift", request.Id);

        await repository.DeleteAsync(shift.Id, cancellationToken);
    }
}
