using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.OperationTemplates;

public class PagedOperationTemplatesResponse(
    IReadOnlyCollection<OperationTemplateResponse> items, int totalCount, int? pageSize)
    : PagedResponse<OperationTemplateResponse>(items, totalCount, pageSize);
