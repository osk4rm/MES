using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// An append-only shift handover logbook entry (issue #293, slice 2/2): the
/// human part of a shift boundary on one Work Center (stored as
/// <see cref="MachineId"/>) — outgoing crew notes linked to the machine and
/// the shift window. The <see cref="ShiftId"/> is resolved from the Work
/// Center calendar at creation time and stays null when the window is
/// uncovered. <see cref="OpenOrdersCount"/> and <see cref="ActiveAndonCount"/>
/// are denormalized context snapshot counts taken at creation time.
/// Corrections are new entries; there is no update or delete path.
/// </summary>
public class ShiftHandover : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Work Center (Machine) the handover belongs to. Stored as a loose Guid.</summary>
    public Guid MachineId { get; set; }

    /// <summary>
    /// Shift resolved from the Work Center calendar covering <see cref="From"/>.
    /// Null when the window has no calendar entry (uncovered shift).
    /// </summary>
    public Guid? ShiftId { get; set; }

    /// <summary>UTC start of the handed-over shift window (immutable after create).</summary>
    public DateTime From { get; set; }

    /// <summary>UTC end of the handed-over shift window (immutable after create).</summary>
    public DateTime To { get; set; }

    /// <summary>Outgoing crew notes. Required, max 2000 characters.</summary>
    public required string Notes { get; set; }

    /// <summary>Denormalized count of open (Released + InProgress) orders at creation time.</summary>
    public int OpenOrdersCount { get; set; }

    /// <summary>Denormalized count of active Andon signals on the machine at creation time.</summary>
    public int ActiveAndonCount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
}
