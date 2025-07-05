using AsistOff.MES.Configuration.Application.Features.Warehouses.Common;
using AsistOff.MES.Multitenancy.Requests;
using ErrorOr;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Get;

public record GetWarehouseRequest(Guid WarehouseId) : ITenantRequest<ErrorOr<WarehouseResult>>;