using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Browse;
using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Responses;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Get;

internal sealed class GetMaintenancePlanRequestHandler(
    IMaintenancePlansRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMaintenancePlanRequest, MaintenancePlanResponse>
{
    public async Task<MaintenancePlanResponse> Handle(GetMaintenancePlanRequest request, CancellationToken cancellationToken)
    {
        var plan = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("MaintenancePlan", request.Id);
        return BrowseMaintenancePlansRequestHandler.Map(plan, dateTimeProvider.UtcNow);
    }
}
