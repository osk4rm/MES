using AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Create;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record CreateOperatorShiftAssignmentRequest(
    Guid OperatorId,
    Guid ShiftId,
    DateOnly? Date,
    string? Notes) : ITenantRequest<OperatorShiftAssignmentResponse>;
