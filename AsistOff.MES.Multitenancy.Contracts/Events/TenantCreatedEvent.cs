using AsistOff.MES.Shared.Abstractions.Events;

namespace AsistOff.MES.Multitenancy.Contracts.Events;

public record TenantCreatedEvent(Guid Id, string Email, string Password) : IEvent;