namespace AsistOff.MES.Multitenancy.Contracts.Interfaces;

public interface ISaasy
{
    Guid TenantId { get; set; }
}