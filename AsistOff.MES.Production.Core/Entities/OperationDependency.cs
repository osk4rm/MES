using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// A directed edge: <see cref="OperationNodeId"/> depends on
/// <see cref="PredecessorOperationNodeId"/>.
/// </summary>
public class OperationDependency : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RecipeVersionId { get; set; }
    public Guid OperationNodeId { get; set; }
    public Guid PredecessorOperationNodeId { get; set; }
    public OperationDependencyType DependencyType { get; set; } = OperationDependencyType.FinishToStart;
    public decimal? LagMinutes { get; set; }

    public virtual OperationNode OperationNode { get; set; } = null!;
    public virtual OperationNode PredecessorOperationNode { get; set; } = null!;
}
