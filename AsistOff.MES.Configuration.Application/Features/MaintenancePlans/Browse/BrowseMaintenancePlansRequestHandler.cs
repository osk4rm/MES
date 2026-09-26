using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Abstractions.Providers;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Browse;

internal sealed class BrowseMaintenancePlansRequestHandler(
    IMaintenancePlansRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<BrowseMaintenancePlansRequest, PagedResponse<MaintenancePlanResponse>>
{
    public async Task<PagedResponse<MaintenancePlanResponse>> Handle(
        BrowseMaintenancePlansRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<MaintenancePlan>(true);

        if (request.MachineId.HasValue)
            predicate = predicate.And(x => x.MachineId == request.MachineId.Value);
        if (request.IsActive.HasValue)
            predicate = predicate.And(x => x.IsActive == request.IsActive.Value);
        if (request.DueBefore.HasValue)
            predicate = predicate.And(x => x.NextDueAt != null && x.NextDueAt < request.DueBefore.Value);

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<MaintenancePlan>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        var nowUtc = dateTimeProvider.UtcNow;
        var result = items.Select(p => Map(p, nowUtc)).ToList();
        return new PagedMaintenancePlansResponse(result, totalCount, request.PageSize);
    }

    internal static MaintenancePlanResponse Map(MaintenancePlan p, DateTime nowUtc) => new(
        p.Id, p.Code, p.Name, p.Description, p.MachineId, p.Machine?.Code,
        p.TriggerType, p.IntervalDays, p.MeterIntervalValue,
        p.NextDueAt, p.LastCompletedAt, p.IsActive,
        MaintenancePlanDueBadge.IsOverdue(p, nowUtc),
        MaintenancePlanDueBadge.DueInDays(p, nowUtc));
}
