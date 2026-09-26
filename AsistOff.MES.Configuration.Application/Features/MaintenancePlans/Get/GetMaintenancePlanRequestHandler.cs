using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Browse;
using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Responses;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Get;

internal sealed class GetMaintenancePlanRequestHandler(IMaintenancePlansRepository repository)
    : IRequestHandler<GetMaintenancePlanRequest, MaintenancePlanResponse>
{
    public async Task<MaintenancePlanResponse> Handle(GetMaintenancePlanRequest request, CancellationToken cancellationToken)
    {
        var plan = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("MaintenancePlan", request.Id);
        return BrowseMaintenancePlansRequestHandler.Map(plan);
    }
}
