using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Browse;
using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Create;

internal sealed class CreateMaintenancePlanRequestHandler(
    IMaintenancePlansRepository repository,
    IMachinesRepository machinesRepository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateMaintenancePlanRequest, MaintenancePlanResponse>
{
    public async Task<MaintenancePlanResponse> Handle(
        CreateMaintenancePlanRequest request, CancellationToken cancellationToken)
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

        var machine = await machinesRepository.GetByIdAsync(request.MachineId, cancellationToken)
            ?? throw new NotFoundException("Machine", request.MachineId);

        if (await repository.CodeExistsAsync(request.Code, null, cancellationToken))
            throw new ConflictException($"Maintenance plan with code '{request.Code}' already exists.");

        var plan = new MaintenancePlan
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            MachineId = machine.Id,
            TriggerType = request.TriggerType,
            IntervalDays = request.IntervalDays,
            MeterIntervalValue = request.MeterIntervalValue,
            NextDueAt = request.NextDueAt,
            LastCompletedAt = null,
            IsActive = request.IsActive
        };

        await repository.AddAsync(plan, cancellationToken);
        plan.Machine = machine;
        return BrowseMaintenancePlansRequestHandler.Map(plan);
    }
}
