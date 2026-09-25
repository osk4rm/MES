using AsistOff.MES.Configuration.Application.Features.Machines.Browse;
using AsistOff.MES.Configuration.Application.Features.Machines.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Machines.Create;

internal sealed class CreateMachineRequestHandler(
    IMachinesRepository repository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateMachineRequest, MachineResponse>
{
    public async Task<MachineResponse> Handle(CreateMachineRequest request, CancellationToken cancellationToken)
    {
        var entity = new Machine
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive,
            Capacity = MachineCapacityRules.ResolveCapacity(request.Capacity),
            EfficiencyFactor = MachineCapacityRules.ResolveEfficiencyFactor(request.EfficiencyFactor),
            DepartmentId = request.DepartmentId,
            SyncId = request.SyncId
        };
        var created = await repository.AddAsync(entity, cancellationToken);
        var withDepartment = await repository.GetByIdAsync(created.Id, cancellationToken) ?? created;
        return BrowseMachinesRequestHandler.Map(withDepartment);
    }
}
