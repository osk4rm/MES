using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Machines.Update;

internal sealed class UpdateMachineRequestHandler(IMachinesRepository repository)
    : IRequestHandler<UpdateMachineRequest>
{
    public async Task Handle(UpdateMachineRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Machine", request.Id);

        entity.Code = request.Code;
        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.IsActive = request.IsActive;
        entity.Capacity = MachineCapacityRules.ResolveCapacity(request.Capacity);
        entity.EfficiencyFactor = MachineCapacityRules.ResolveEfficiencyFactor(request.EfficiencyFactor);
        entity.DepartmentId = request.DepartmentId;
        entity.SyncId = request.SyncId;

        await repository.UpdateAsync(entity, cancellationToken);
    }
}
