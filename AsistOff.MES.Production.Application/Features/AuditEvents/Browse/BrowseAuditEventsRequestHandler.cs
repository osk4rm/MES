using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.AuditEvents.Browse;

internal sealed class BrowseAuditEventsRequestHandler(
    IAuditEventsRepository repository,
    ICurrentPermissionsAccessor permissionsAccessor)
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

        // Issue #372: read-only callers (production.read without
        // production.write) see that an event happened but not the raw change
        // payload — it can carry actor-adjacent, order and machine detail.
        if (!SensitiveProjectionPolicy.CanSeeSensitiveDetails(permissionsAccessor.Permissions))
            items = items.Select(x => x with { Payload = null }).ToList();

        return new PagedAuditEventsResponse(items, total, request.PageSize);
    }
}
