using AsistOff.MES.Multitenancy.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

public class Department : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    
    public virtual ICollection<Operator> Employees { get; set; } = new List<Operator>();
}