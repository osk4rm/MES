using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

public class ProductMeasureUnit : IEntity, ISaasy, ISyncable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? SyncId { get; set; }
    public required Guid ProductId { get; set; }
    public required Guid MeasureUnitId { get; set; }
    public decimal ConversionFactor { get; set; } = 1;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    
    public virtual Product Product { get; set; } = null!;
    public virtual MeasureUnit MeasureUnit { get; set; } = null!;
}