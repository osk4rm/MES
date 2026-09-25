using AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Create;

public record CreateOperatorShiftAssignmentRequest(
    Guid OperatorId,
    Guid ShiftId,
    DateOnly? Date,
    string? Notes) : ITenantRequest<OperatorShiftAssignmentResponse>;
