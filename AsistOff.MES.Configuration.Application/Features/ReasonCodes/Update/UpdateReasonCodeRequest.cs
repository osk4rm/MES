using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.ReasonCodes.Update;

public record UpdateReasonCodeRequest(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    ReasonCodeCategory Category,
    bool IsActive,
    int SortIndex) : ITenantRequest;
