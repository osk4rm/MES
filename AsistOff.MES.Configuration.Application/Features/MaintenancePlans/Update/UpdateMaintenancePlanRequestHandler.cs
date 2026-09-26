using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Update;

internal sealed class UpdateMaintenancePlanRequestHandler(
    IMaintenancePlansRepository repository,
    IMachinesRepository machinesRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateMaintenancePlanRequest>
{
    public async Task Handle(UpdateMaintenancePlanRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ValidationException(nameof(request.Code), "Code is required");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException(nameof(request.Name), "Name is required");

        MaintenancePlanRules.ValidateSchedule(
            request.TriggerType,
            request.IntervalDays,
            request.MeterIntervalValue,
            request.NextDueAt,
            dateTimeProvider.UtcNow);

        var plan = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("MaintenancePlan", request.Id);

        var machine = await machinesRepository.GetByIdAsync(request.MachineId, cancellationToken)
            ?? throw new NotFoundException("Machine", request.MachineId);

        if (await repository.CodeExistsAsync(request.Code, request.Id, cancellationToken))
            throw new ConflictException($"Maintenance plan with code '{request.Code}' already exists.");

        plan.Code = request.Code;
        plan.Name = request.Name;
        plan.Description = request.Description;
        plan.MachineId = machine.Id;
        plan.TriggerType = request.TriggerType;
        plan.IntervalDays = request.IntervalDays;
        plan.MeterIntervalValue = request.MeterIntervalValue;
        plan.NextDueAt = request.NextDueAt;
        plan.IsActive = request.IsActive;

        await repository.UpdateAsync(plan, cancellationToken);
    }
}
