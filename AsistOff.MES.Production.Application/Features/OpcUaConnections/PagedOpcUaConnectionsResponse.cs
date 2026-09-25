using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections;

public sealed class PagedOpcUaConnectionsResponse(
    IReadOnlyCollection<OpcUaConnectionResponse> items,
    int totalCount,
    int? pageSize)
    : PagedResponse<OpcUaConnectionResponse>(items, totalCount, pageSize);
