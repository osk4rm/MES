using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Get;

public record GetOperatorShiftAssignmentRequest(Guid Id) : ITenantRequest<Responses.OperatorShiftAssignmentResponse>;
