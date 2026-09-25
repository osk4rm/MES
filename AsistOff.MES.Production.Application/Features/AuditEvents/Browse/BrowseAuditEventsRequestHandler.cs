using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.AuditEvents.Browse;

internal sealed class BrowseAuditEventsRequestHandler(IAuditEventsRepository repository)
    : IRequestHandler<BrowseAuditEventsRequest, PagedResponse<AuditEventResponse>>
{
    public async Task<PagedResponse<AuditEventResponse>> Handle(BrowseAuditEventsRequest request, CancellationToken cancellationToken)
    {
        var total = await repository.CountAsync(request.EntityName, request.EntityId, cancellationToken);

        var pageNumber = request.PageNumber ?? 1;
        var pageSize = request.PageSize ?? 10;
        var items = await repository.BrowseAsync(
            request.EntityName,
            request.EntityId,
            (pageNumber - 1) * pageSize,
            pageSize,
            cancellationToken);

        return new PagedAuditEventsResponse(items, total, request.PageSize);
    }
}
