using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.ResourceRequirements.Add;

public record AddResourceRequirementRequest(
    Guid OperationId,
    Guid? PreferredDepartmentId,
    Guid? PreferredMachineId,
    string? RequiredCapability,
    int RequiredOperatorCount,
    string? RequiredRole,
    string? Notes) : ITenantRequest<Guid>;
