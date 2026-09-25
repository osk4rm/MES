using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.ResourceRequirements.Update;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record UpdateResourceRequirementRequest(
    Guid ResourceRequirementId,
    Guid? PreferredDepartmentId,
    Guid? PreferredMachineId,
    string? RequiredCapability,
    int RequiredOperatorCount,
    string? RequiredRole,
    string? Notes) : ITenantRequest;
