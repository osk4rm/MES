using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Machines.Delete;

internal sealed class DeleteMachineRequestHandler(IMachinesRepository repository)
    : IRequestHandler<DeleteMachineRequest>
{
    public async Task Handle(DeleteMachineRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Machine", request.Id);
        await repository.DeleteAsync(entity.Id, cancellationToken);
    }
}
