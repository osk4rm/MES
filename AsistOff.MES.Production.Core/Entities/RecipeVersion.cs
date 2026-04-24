using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// A versioned snapshot of a recipe's operational content. Only versions
/// in <see cref="RecipeVersionStatus.Draft"/> are mutable.
/// </summary>
public class RecipeVersion : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RecipeId { get; set; }
    public int VersionNumber { get; set; }
    public RecipeVersionStatus Status { get; set; } = RecipeVersionStatus.Draft;
    public DateTime? ReleasedAt { get; set; }
    public Guid? ReleasedByUserId { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string? ChangeNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual Recipe Recipe { get; set; } = null!;
    public virtual ICollection<OperationNode> Operations { get; set; } = new List<OperationNode>();
}
