using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.ResourceRequirements.Update;

public record UpdateResourceRequirementRequest(
    Guid ResourceRequirementId,
    Guid? PreferredDepartmentId,
    Guid? PreferredMachineId,
    string? RequiredCapability,
    int RequiredOperatorCount,
    string? RequiredRole,
    string? Notes) : ITenantRequest;
