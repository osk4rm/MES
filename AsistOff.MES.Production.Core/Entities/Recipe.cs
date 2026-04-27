using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// A recipe is a stable logical identity (e.g. "Production of X"). The actual
/// content (operations, BOM, timings) belongs to its <see cref="RecipeVersion"/>s.
/// </summary>
public class Recipe : IEntity, ISaasy, ISyncable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? SyncId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional informational pointer to the product that is considered the
    /// primary output of this recipe. Not enforced at the domain level — a
    /// recipe can produce several products per operation.
    /// </summary>
    public Guid? PrimaryProductId { get; set; }

    /// <summary>
    /// Points to the currently released version, if any. A recipe without a
    /// released version cannot be used by production orders.
    /// </summary>
    public Guid? CurrentVersionId { get; set; }

    public virtual ICollection<RecipeVersion> Versions { get; set; } = new List<RecipeVersion>();
}
