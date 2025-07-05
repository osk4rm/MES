using AsistOff.MES.Configuration.Application.Features.Warehouses.Common;
using AsistOff.MES.Multitenancy.Requests;
using ErrorOr;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Create;

public record CreateWarehouseRequest(string Name, string? SyncId) : ITenantRequest<ErrorOr<WarehouseResult>>;