using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

public class Warehouse : IEntity, ISaasy, ISyncable
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required Guid TenantId { get; set; }
    public string? SyncId { get; set; }
}