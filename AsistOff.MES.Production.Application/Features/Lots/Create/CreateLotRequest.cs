using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.Lots.Create;

public record CreateLotRequest(
    string Code,
    Guid ProductId,
    Guid MeasureUnitId,
    decimal Quantity,
    string? SupplierLotNumber,
    DateTime? ProducedAt,
    DateTime? ExpiryDate,
    string? Notes) : ITenantRequest<LotResponse>;
