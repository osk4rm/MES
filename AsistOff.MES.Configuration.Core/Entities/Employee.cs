using AsistOff.MES.Multitenancy.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

public class Employee : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Identifier { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required decimal RatePerHour { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid UserId { get; set; }
    
    public virtual Department Department { get; set; } = null!;
}