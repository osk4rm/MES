namespace AsistOff.MES.Configuration.Api.Contracts.Warehouses;

public record WarehousesResponse(
    IReadOnlyCollection<WarehouseResponse> Warehouses
);