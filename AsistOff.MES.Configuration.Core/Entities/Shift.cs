using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

/// <summary>
/// A named working-time window (e.g. <c>06:00–14:00</c>) that can be attached to
/// the weekly entries of a Work Center calendar. When <see cref="EndTime"/> is
/// not after <see cref="StartTime"/> the shift crosses midnight
/// (e.g. <c>22:00–06:00</c>).
/// </summary>
public class Shift : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsActive { get; set; } = true;
}
