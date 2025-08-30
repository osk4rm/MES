namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Common.Responses;

public record WarehouseResponse(
    Guid Id,
    string Name,
    string? SyncId
);
