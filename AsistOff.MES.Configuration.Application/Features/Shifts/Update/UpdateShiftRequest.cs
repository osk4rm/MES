using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Shifts.Update;

public record UpdateShiftRequest(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsActive) : ITenantRequest;
