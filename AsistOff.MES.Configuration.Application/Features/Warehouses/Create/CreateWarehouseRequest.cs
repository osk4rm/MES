using AsistOff.MES.Configuration.Application.Features.Warehouses.Common;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Create;

public record CreateWarehouseRequest(string Name, string SyncId) : ITenantRequest<WarehouseResult>;
