using AsistOff.MES.Multitenancy.Contracts.Events;
using AsistOff.MES.Shared.Abstractions.Events;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Users.Application.EventListeners;

public sealed class TenantCreatedEventListener(
    IUsersRepository usersRepository,
    IGuidProvider guidProvider,
    ILogger<TenantCreatedEventListener> logger)
    : IEventListener<TenantCreatedEvent>
{
    public async Task HandleAsync(TenantCreatedEvent @event)
    {
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