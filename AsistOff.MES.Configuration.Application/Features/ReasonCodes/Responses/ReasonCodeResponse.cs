using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.ReasonCodes.Responses;

public record ReasonCodeResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    ReasonCodeCategory Category,
    bool IsActive,
    int SortIndex);

public class PagedReasonCodesResponse(
    IReadOnlyCollection<ReasonCodeResponse> items,
    int totalCount,
    int? pageSize)
    : PagedResponse<ReasonCodeResponse>(items, totalCount, pageSize);
