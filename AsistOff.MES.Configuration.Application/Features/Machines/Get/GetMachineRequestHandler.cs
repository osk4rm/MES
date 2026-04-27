using AsistOff.MES.Configuration.Application.Features.Machines.Browse;
using AsistOff.MES.Configuration.Application.Features.Machines.Responses;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Machines.Get;

internal sealed class GetMachineRequestHandler(IMachinesRepository repository)
    : IRequestHandler<GetMachineRequest, MachineResponse>
{
    public async Task<MachineResponse> Handle(GetMachineRequest request, CancellationToken cancellationToken)
    {
        var machine = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Machine", request.Id);
        return BrowseMachinesRequestHandler.Map(machine);
    }
}
