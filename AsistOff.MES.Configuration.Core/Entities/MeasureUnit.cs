using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

public class MeasureUnit : IEntity, ISaasy, ISyncable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? SyncId { get; set; }
    public required string Name { get; set; }
    public required string Symbol { get; set; }
    public MeasureUnitType Type { get; set; }
    public decimal? ConversionFactor { get; set; }
    public Guid? BaseUnitId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }

    public virtual MeasureUnit? BaseUnit { get; set; }
    public virtual ICollection<MeasureUnit> DerivedUnits { get; set; } = new List<MeasureUnit>();
    public virtual ICollection<ProductMeasureUnit> ProductMeasureUnits { get; set; } = new List<ProductMeasureUnit>();
}