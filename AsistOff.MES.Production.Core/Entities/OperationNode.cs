using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// A single node in the recipe's DAG. Execution order is inferred from
/// <see cref="OperationDependency"/> edges, not from <see cref="SortIndex"/>,
/// which is a UI-only hint.
/// </summary>
public class OperationNode : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RecipeVersionId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? OperationType { get; set; }
    public int SortIndex { get; set; }

    // Timing components (all optional, allowing maximum flexibility)
    public decimal? SetupTimeMinutes { get; set; }
    public RunTimeMode RunTimeMode { get; set; } = RunTimeMode.PerUnitSeconds;
    public decimal? RunTimePerUnitSeconds { get; set; }
    public decimal? RunTimePerBatchMinutes { get; set; }
    public decimal? TeardownTimeMinutes { get; set; }
    public decimal? QueueTimeMinutes { get; set; }

    public bool IsOptional { get; set; }
    public bool AllowParallelExecution { get; set; }
    public decimal? ExpectedQuantity { get; set; }

    public virtual RecipeVersion RecipeVersion { get; set; } = null!;

    // Edges in which this node is the successor (i.e. it depends on predecessors)
    public virtual ICollection<OperationDependency> Dependencies { get; set; } = new List<OperationDependency>();

    public virtual ICollection<BomItem> BomItems { get; set; } = new List<BomItem>();
    public virtual ICollection<OperationOutput> Outputs { get; set; } = new List<OperationOutput>();
    public virtual ICollection<ResourceRequirement> ResourceRequirements { get; set; } = new List<ResourceRequirement>();
}
