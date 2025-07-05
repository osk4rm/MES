using System.ComponentModel.DataAnnotations.Schema;

namespace AsistOff.MES.Multitenancy.Entity;

public class Tenant
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; }
    
    [Column(TypeName = "jsonb")]
    public string Settings { get; set; }
}