using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.Lots;

public record LotResponse(
    Guid Id,
    string Code,
    Guid ProductId,
    Guid MeasureUnitId,
    decimal Quantity,
    LotStatus Status,
    string? SupplierLotNumber,
    DateTime? ProducedAt,
    DateTime? ExpiryDate,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
