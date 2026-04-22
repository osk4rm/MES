using AsistOff.MES.Configuration.Application.Features.Operators.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Operators.Get;

public record GetOperatorRequest(Guid OperatorId) : ITenantRequest<OperatorResponse>;
