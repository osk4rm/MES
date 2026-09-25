using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// A tenant-scoped batch of identical units produced together. Lots are the
/// smallest traceability increment: they can be looked up by code (e.g. via
/// shop-floor scanning) and carry a lifecycle status. Linking lots to
/// confirmations, RW/PW movements and genealogy edges is a follow-up.
/// </summary>
public class Lot : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Code { get; set; }
    public Guid ProductId { get; set; }
    public Guid MeasureUnitId { get; set; }
    public decimal Quantity { get; set; }
    public LotStatus Status { get; set; } = LotStatus.Available;
    public string? SupplierLotNumber { get; set; }
    public DateTime? ProducedAt { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
}
