using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Delete;

internal sealed class DeleteMaintenancePlanRequestHandler(IMaintenancePlansRepository repository)
    : IRequestHandler<DeleteMaintenancePlanRequest>
{
    public async Task Handle(DeleteMaintenancePlanRequest request, CancellationToken cancellationToken)
    {
        var plan = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("MaintenancePlan", request.Id);
        await repository.DeleteAsync(plan.Id, cancellationToken);
    }
}
