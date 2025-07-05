using AsistOff.MES.Configuration.Application.Features.Warehouses.Common;
using MediatR;
using ErrorOr;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Browse;

public record BrowseWarehousesRequest : IRequest<ErrorOr<IReadOnlyCollection<WarehouseResult>>>;