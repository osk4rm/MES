using AsistOff.MES.Shared.Abstractions.Events;
using AsistOff.MES.Shared.Abstractions.Models.DomainEvents;

namespace AsistOff.MES.Multitenancy.Contracts.Events;

/// <summary>
/// Cross-module fact emitted when a tenant self-registers (slice 3, #260).
/// Implements <see cref="IDomainEvent"/> (in addition to <see cref="IEvent"/>)
/// so the transactional-outbox relay can resolve the stored type, deserialize
/// the stored payload and deliver it through MediatR strictly after the tenant
/// row commits. The legacy <see cref="IEventListener{TEvent}"/> path is kept
/// for compatibility; both entries converge on the same idempotent listener.
/// Tenant binding always comes from <see cref="Id"/> (the committed tenant
/// row), never from caller input, so redelivery cannot cross tenant
/// boundaries.
/// </summary>
/// <param name="Id">Id of the committed tenant row; also the outbox row's tenant scope.</param>
/// <param name="Email">Tenant admin contact email; the provisioned admin login.</param>
/// <param name="HashedPassword">
/// One-way password hash for the provisioned admin (never plaintext: the
/// handler hashes before staging, so the outbox payload carries no secret
/// material). Required downstream — the listener cannot provision the admin
/// login without it.
/// </param>
public record TenantCreatedEvent(Guid Id, string Email, string HashedPassword) : IEvent, IDomainEvent;