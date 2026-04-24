using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// Preferred resources to carry out the operation. At the recipe level, only
/// preferences are set — concrete operators/machines are assigned by production
/// orders / scheduling.
/// </summary>
public class ResourceRequirement : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid OperationNodeId { get; set; }
    public Guid? PreferredDepartmentId { get; set; }
    public Guid? PreferredMachineId { get; set; }
    public string? RequiredCapability { get; set; }
    public int RequiredOperatorCount { get; set; } = 1;
    public string? RequiredRole { get; set; }
    public string? Notes { get; set; }

    public virtual OperationNode OperationNode { get; set; } = null!;
}
