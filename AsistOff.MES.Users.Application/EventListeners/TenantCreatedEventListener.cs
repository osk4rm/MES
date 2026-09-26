using AsistOff.MES.Multitenancy.Contracts.Events;
using AsistOff.MES.Shared.Abstractions.Events;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Rbac;
using AsistOff.MES.Users.Core.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Users.Application.EventListeners;

/// <summary>
/// Provisions the RBAC parity rows and the tenant-admin user for a freshly
/// created tenant (slice 3, #260). Two entries converge here: the legacy
/// <see cref="IEventListener{TEvent}"/> direct path and the MediatR
/// <see cref="INotificationHandler{TNotification}"/> relay path through which
/// the transactional outbox delivers the staged <see cref="TenantCreatedEvent"/>
/// after the tenant row commits.
/// Redelivery is idempotent: the provisioner reconciles by explicit tenant id
/// (no duplicates on repeat runs) and an already-provisioned admin email is
/// skipped, so dispatching the same event twice provisions exactly once.
/// Malformed events (empty tenant id, blank email) are rejected with a
/// <see cref="ValidationException"/> before any provisioning, so a poisoned
/// relay row parks without partial RBAC writes. Tenant binding always comes
/// from the event payload (the committed tenant row), never from caller
/// input, so handling can never provision under a foreign tenant.
/// </summary>
public sealed class TenantCreatedEventListener(
    IUsersRepository usersRepository,
    IRbacProvisioner rbacProvisioner,
    IGuidProvider guidProvider,
    ILogger<TenantCreatedEventListener> logger)
    : IEventListener<TenantCreatedEvent>, INotificationHandler<TenantCreatedEvent>
{
    public Task Handle(TenantCreatedEvent notification, CancellationToken cancellationToken)
        => HandleCoreAsync(notification, cancellationToken);

    public Task HandleAsync(TenantCreatedEvent @event)
        => HandleCoreAsync(@event);

    private async Task HandleCoreAsync(TenantCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event.Id == Guid.Empty)
        {
            throw new ValidationException(
                nameof(TenantCreatedEvent.Id),
                "Cannot provision a tenant from a tenant-created event without a tenant id.");
        }

        if (string.IsNullOrWhiteSpace(@event.Email))
        {
            throw new ValidationException(
                nameof(TenantCreatedEvent.Email),
                "Cannot provision a tenant admin user from a tenant-created event without an email.");
        }

        // Provision parity RBAC rows first so a fresh tenant always has
        // tenant_admin/user roles even if user creation is skipped as duplicate.
        await rbacProvisioner.ProvisionAsync(@event.Id, cancellationToken);

        var existing = await usersRepository.GetByEmailAndTenantIgnoringQueryFiltersAsync(@event.Email, @event.Id, cancellationToken);
        if (existing is not null)
        {
            logger.LogWarning("Tenant admin user for email {Email} already exists for tenant {TenantId}, skipping creation", @event.Email, @event.Id);
            return;
        }

        var user = new User
        {
            Id = guidProvider.NewGuid(),
            IsTenantAdmin = true,
            Email = @event.Email,
            TenantId = @event.Id,
            Password = @event.HashedPassword,
        };

        await usersRepository.AddAsync(user);
    }
}
