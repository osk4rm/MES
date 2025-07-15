using AsistOff.MES.Multitenancy.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

public class Department : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
}