using System.Collections.Immutable;
using AsistOff.MES.Configuration.Application.Features.Warehouses.Browse;
using AsistOff.MES.Configuration.Application.Features.Warehouses.Common;

namespace AsistOff.MES.Configuration.Api.Contracts.Warehouses;

public record WarehouseResponse(
    Guid Id,
    string Name,
    string? ExternalId
    );

public static class WarehouseResponseExtensions
{
    public static WarehouseResponse ToResponse(this WarehouseResult warehouse) =>
        new WarehouseResponse(warehouse.Id, warehouse.Name, warehouse.SyncId);
    
    public static IReadOnlyCollection<WarehouseResponse> ToResponse(this IReadOnlyCollection<WarehouseResult> warehouses) =>
        warehouses.Select(x => new WarehouseResponse(x.Id, x.Name, x.SyncId)).ToImmutableList();
}