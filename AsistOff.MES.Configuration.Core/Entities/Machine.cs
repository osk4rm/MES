using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

public class Machine : IEntity, ISaasy, ISyncable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? SyncId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? DepartmentId { get; set; }

    public virtual Department? Department { get; set; }
}
