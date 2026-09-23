using AsistOff.MES.Configuration.Application.Features.ReasonCodes.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.ReasonCodes.Get;

public record GetReasonCodeRequest(Guid Id) : ITenantRequest<ReasonCodeResponse>;
