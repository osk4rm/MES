using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Users.Core.Entities;

/// <summary>
/// Tenant-scoped link between a <see cref="User"/> and a <see cref="Role"/>.
/// </summary>
public class UserRole : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }

    public virtual User? User { get; set; }
    public virtual Role? Role { get; set; }
}
