using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Shifts.Update;

internal sealed class UpdateShiftRequestHandler(IShiftsRepository repository)
    : IRequestHandler<UpdateShiftRequest>
{
    public async Task Handle(UpdateShiftRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ValidationException(nameof(request.Code), "Code is required");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException(nameof(request.Name), "Name is required");

        var shift = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Shift", request.Id);

        if (await repository.CodeExistsAsync(request.Code, request.Id, cancellationToken))
            throw new ConflictException($"Shift with code '{request.Code}' already exists.");

        shift.Code = request.Code;
        shift.Name = request.Name;
        shift.Description = request.Description;
        shift.StartTime = request.StartTime;
        shift.EndTime = request.EndTime;
        shift.IsActive = request.IsActive;

        await repository.UpdateAsync(shift, cancellationToken);
    }
}
