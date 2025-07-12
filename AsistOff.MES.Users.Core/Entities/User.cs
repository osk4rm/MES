using AsistOff.MES.Multitenancy.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Users.Core.Entities;

public class User : ISaasy, IEntity, IAuditable
{
    public Guid Id { get; set; }
    public required Guid TenantId { get; set; }
    public required string Password { get; set; }
    public required string Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsTenantAdmin { get; set; }
}