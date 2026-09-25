using AsistOff.MES.Multitenancy.Contracts.Events;
using AsistOff.MES.Shared.Abstractions.Events;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Rbac;
using AsistOff.MES.Users.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Users.Application.EventListeners;

public sealed class TenantCreatedEventListener(
    IUsersRepository usersRepository,
    IRbacProvisioner rbacProvisioner,
    IGuidProvider guidProvider,
    ILogger<TenantCreatedEventListener> logger)
    : IEventListener<TenantCreatedEvent>
{
    public async Task HandleAsync(TenantCreatedEvent @event)
    {
        // Provision parity RBAC rows first so a fresh tenant always has
        // tenant_admin/user roles even if user creation is skipped as duplicate.
        await rbacProvisioner.ProvisionAsync(@event.Id);

        var existing = await usersRepository.GetByEmailAndTenantIgnoringQueryFiltersAsync(@event.Email, @event.Id);
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