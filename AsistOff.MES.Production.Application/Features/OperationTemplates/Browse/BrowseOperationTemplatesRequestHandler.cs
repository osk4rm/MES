using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OperationTemplates.Browse;

internal sealed class BrowseOperationTemplatesRequestHandler(IOperationTemplatesRepository repository)
    : IRequestHandler<BrowseOperationTemplatesRequest, PagedResponse<OperationTemplateResponse>>
{
    public async Task<PagedResponse<OperationTemplateResponse>> Handle(BrowseOperationTemplatesRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<OperationTemplate>(true);

        if (!string.IsNullOrWhiteSpace(request.Name))
            predicate = predicate.And(x => x.Name.Contains(request.Name));
        if (!string.IsNullOrWhiteSpace(request.Code))
            predicate = predicate.And(x => x.Code.Contains(request.Code));
        if (request.IsActive.HasValue)
            predicate = predicate.And(x => x.IsActive == request.IsActive);

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<OperationTemplate>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        return new PagedOperationTemplatesResponse(
            items.Select(Map).ToList(), totalCount, request.PageSize);
    }

    internal static OperationTemplateResponse Map(OperationTemplate t) => new(
        t.Id, t.Code, t.Name, t.Description, t.OperationType, t.IsActive,
        t.SetupTimeMinutes, t.RunTimeMode, t.RunTimePerUnitSeconds, t.RunTimePerBatchMinutes,
        t.TeardownTimeMinutes, t.QueueTimeMinutes, t.CreatedAt, t.UpdatedAt);
}
