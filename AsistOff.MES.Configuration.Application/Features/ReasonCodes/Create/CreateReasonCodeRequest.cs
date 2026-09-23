using AsistOff.MES.Configuration.Application.Features.ReasonCodes.Responses;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.ReasonCodes.Create;

public record CreateReasonCodeRequest(
    string Code,
    string Name,
    string? Description,
    ReasonCodeCategory Category,
    bool IsActive,
    int SortIndex) : ITenantRequest<ReasonCodeResponse>;
