using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags;

public class PagedMachineTelemetryTagsResponse(
    IReadOnlyCollection<MachineTelemetryTagResponse> items, int totalCount, int? pageSize)
    : PagedResponse<MachineTelemetryTagResponse>(items, totalCount, pageSize);
