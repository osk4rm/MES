using AsistOff.MES.Configuration.Application.Features.Warehouses.Common;
using AsistOff.MES.Multitenancy.Requests;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Get;

public record GetWarehouseRequest(Guid WarehouseId) : ITenantRequest<WarehouseResult>;
