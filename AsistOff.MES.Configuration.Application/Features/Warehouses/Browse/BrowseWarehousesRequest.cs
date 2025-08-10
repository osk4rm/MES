using AsistOff.MES.Configuration.Application.Features.Warehouses.Common;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Browse;

public record BrowseWarehousesRequest : IRequest<IReadOnlyCollection<WarehouseResult>>;
