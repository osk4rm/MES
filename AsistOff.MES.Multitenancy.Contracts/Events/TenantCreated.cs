using AsistOff.MES.Shared.Abstractions.Events;

namespace AsistOff.MES.Multitenancy.Contracts.Events;

public record TenantCreated(Guid Id, string Email) : IEvent;