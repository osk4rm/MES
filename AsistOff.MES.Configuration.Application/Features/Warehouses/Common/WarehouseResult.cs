namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Common;

public record WarehouseResult(
    Guid Id,
    string Name,
    string? SyncId);