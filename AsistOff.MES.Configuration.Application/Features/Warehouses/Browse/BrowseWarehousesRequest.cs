using AsistOff.MES.Configuration.Application.Features.Warehouses.Common;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Browse;

public record BrowseWarehousesRequest : ITenantRequest<IReadOnlyCollection<WarehouseResult>>;
