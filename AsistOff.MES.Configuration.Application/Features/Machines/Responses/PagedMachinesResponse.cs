using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.Machines.Responses;

public class PagedMachinesResponse(IReadOnlyCollection<MachineResponse> items, int totalCount, int? pageSize)
    : PagedResponse<MachineResponse>(items, totalCount, pageSize);
