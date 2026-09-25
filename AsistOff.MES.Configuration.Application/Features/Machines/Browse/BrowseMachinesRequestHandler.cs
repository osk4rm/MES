using AsistOff.MES.Configuration.Application.Features.Machines.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Machines.Browse;

internal sealed class BrowseMachinesRequestHandler(IMachinesRepository repository)
    : IRequestHandler<BrowseMachinesRequest, PagedResponse<MachineResponse>>
{
    public async Task<PagedResponse<MachineResponse>> Handle(BrowseMachinesRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<Machine>(true);

        if (!string.IsNullOrWhiteSpace(request.Name))
            predicate = predicate.And(x => x.Name.Contains(request.Name));
        if (!string.IsNullOrWhiteSpace(request.Code))
            predicate = predicate.And(x => x.Code.Contains(request.Code));
        if (request.IsActive.HasValue)
            predicate = predicate.And(x => x.IsActive == request.IsActive);
        if (request.DepartmentId.HasValue)
            predicate = predicate.And(x => x.DepartmentId == request.DepartmentId);

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<Machine>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        var result = items.Select(Map).ToList();
        return new PagedMachinesResponse(result, totalCount, request.PageSize);
    }

    internal static MachineResponse Map(Machine m) => new(
        m.Id, m.Code, m.Name, m.Description, m.IsActive, m.Capacity, m.EfficiencyFactor,
        m.DepartmentId, m.Department?.Code, m.Department?.Name, m.SyncId);
}
