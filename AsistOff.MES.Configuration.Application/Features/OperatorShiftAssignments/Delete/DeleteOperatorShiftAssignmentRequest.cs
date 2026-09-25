using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Delete;

public record DeleteOperatorShiftAssignmentRequest(Guid Id) : ITenantRequest;
