using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.ResourceRequirements.Remove;

public record RemoveResourceRequirementRequest(Guid ResourceRequirementId) : ITenantRequest;
