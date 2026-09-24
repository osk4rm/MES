using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.AndonSignals.Update;

public record UpdateAndonSignalRequest(
    Guid Id,
    AndonSignalCategory Category,
    Guid? ReasonCodeId,
    string? Notes) : ITenantRequest<AndonSignalResponse>;
