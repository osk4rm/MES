using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.MaterialReservations;

public class PagedMaterialReservationsResponse(
    IReadOnlyCollection<MaterialReservationResponse> items, int totalCount, int? pageSize)
    : PagedResponse<MaterialReservationResponse>(items, totalCount, pageSize);
