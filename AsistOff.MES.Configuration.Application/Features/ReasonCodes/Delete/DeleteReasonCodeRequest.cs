using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.ReasonCodes.Delete;

public record DeleteReasonCodeRequest(Guid Id) : ITenantRequest;
