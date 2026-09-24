using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Lots.Update;

public record UpdateLotRequest(
    Guid Id,
    string Code,
    Guid ProductId,
    Guid MeasureUnitId,
    decimal Quantity,
    string? SupplierLotNumber,
    DateTime? ProducedAt,
    DateTime? ExpiryDate,
    string? Notes) : ITenantRequest;
