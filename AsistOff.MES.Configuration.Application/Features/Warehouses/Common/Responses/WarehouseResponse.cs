namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Common.Responses;

public record WarehouseItemResponse(
    Guid Id,
    string Name,
    string? SyncId
);
