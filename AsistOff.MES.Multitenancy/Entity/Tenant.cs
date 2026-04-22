using System.ComponentModel.DataAnnotations.Schema;

namespace AsistOff.MES.Multitenancy.Entity;

public class Tenant
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; }
    
    [Column(TypeName = "jsonb")]
    public TenantSettings Settings { get; set; } = new();
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? ContactEmail { get; set; }
}