using AsistOff.MES.Configuration.Application.Features.ReasonCodes.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.ReasonCodes.Browse;

internal sealed class BrowseReasonCodesRequestHandler(IReasonCodesRepository repository)
    : IRequestHandler<BrowseReasonCodesRequest, PagedResponse<ReasonCodeResponse>>
{
    public async Task<PagedResponse<ReasonCodeResponse>> Handle(
        BrowseReasonCodesRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<ReasonCode>(true);

        if (!string.IsNullOrWhiteSpace(request.Code))
            predicate = predicate.And(x => x.Code.Contains(request.Code));
        if (!string.IsNullOrWhiteSpace(request.Name))
            predicate = predicate.And(x => x.Name.Contains(request.Name));
        if (request.Category.HasValue)
            predicate = predicate.And(x => x.Category == request.Category);
        if (request.IsActive.HasValue)
            predicate = predicate.And(x => x.IsActive == request.IsActive);

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<ReasonCode>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        var result = items.Select(Map).ToList();
        return new PagedReasonCodesResponse(result, totalCount, request.PageSize);
    }

    internal static ReasonCodeResponse Map(ReasonCode r) =>
        new(r.Id, r.Code, r.Name, r.Description, r.Category, r.IsActive, r.SortIndex);
}
