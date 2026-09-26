using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Browse;
using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Responses;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Due;

internal sealed class GetDueMaintenancePlansRequestHandler(
    IMaintenancePlansRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetDueMaintenancePlansRequest, IReadOnlyCollection<MaintenancePlanResponse>>
{
    public async Task<IReadOnlyCollection<MaintenancePlanResponse>> Handle(
        GetDueMaintenancePlansRequest request, CancellationToken cancellationToken)
    {
        var nowUtc = dateTimeProvider.UtcNow;
        var horizon = request.DueWithinDays.HasValue
            ? nowUtc.AddDays(request.DueWithinDays.Value)
            : (DateTime?)null;

        // ListActiveAsync is tenant-scoped via the global query filter.
        var plans = await repository.ListActiveAsync(cancellationToken);

        return plans
            .Where(p => p.NextDueAt.HasValue)
            .Where(p => !request.OverdueOnly.GetValueOrDefault() || p.NextDueAt!.Value <= nowUtc)
            .Where(p => horizon == null || p.NextDueAt!.Value <= horizon.Value)
            .OrderBy(p => p.NextDueAt!.Value)
            .Select(p => BrowseMaintenancePlansRequestHandler.Map(p, nowUtc))
            .ToList();
    }
}
