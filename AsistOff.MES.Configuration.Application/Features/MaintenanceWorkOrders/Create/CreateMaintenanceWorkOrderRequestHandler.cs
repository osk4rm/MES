using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Browse;
using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Create;

internal sealed class CreateMaintenanceWorkOrderRequestHandler(
    IMaintenanceWorkOrdersRepository repository,
    IMachinesRepository machinesRepository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateMaintenanceWorkOrderRequest, MaintenanceWorkOrderResponse>
{
    public async Task<MaintenanceWorkOrderResponse> Handle(
        CreateMaintenanceWorkOrderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ValidationException(nameof(request.Code), "Code is required");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ValidationException(nameof(request.Title), "Title is required");
        if (!Enum.IsDefined(request.Priority))
            throw new ValidationException(nameof(request.Priority), "Priority is invalid");

        var machine = await machinesRepository.GetByIdAsync(request.MachineId, cancellationToken)
            ?? throw new NotFoundException("Machine", request.MachineId);

        if (await repository.CodeExistsAsync(request.Code, null, cancellationToken))
            throw new ConflictException($"Maintenance work order with code '{request.Code}' already exists.");

        var workOrder = new MaintenanceWorkOrder
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            Code = request.Code,
            Title = request.Title,
            Description = request.Description,
            MachineId = machine.Id,
            Priority = request.Priority,
            Status = MaintenanceWorkOrderStatus.Open,
            ReportedAt = dateTimeProvider.UtcNow
        };

        await repository.AddAsync(workOrder, cancellationToken);
        workOrder.Machine = machine;
        return BrowseMaintenanceWorkOrdersRequestHandler.Map(workOrder);
    }
}
