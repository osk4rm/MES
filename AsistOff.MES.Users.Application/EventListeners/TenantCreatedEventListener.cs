using AsistOff.MES.Multitenancy.Contracts.Events;
using AsistOff.MES.Shared.Abstractions.Events;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using Microsoft.AspNetCore.Identity;

namespace AsistOff.MES.Users.Application.EventListeners;

public sealed class TenantCreatedEventListener(
    IUsersRepository usersRepository, 
    IPasswordHasher<User> passwordHasher)
    : IEventListener<TenantCreatedEvent>
{
    public async Task HandleAsync(TenantCreatedEvent @event)
    {
        var user = new User()
        {
            IsTenantAdmin = true,
            Email = @event.Email,
            TenantId = @event.Id,
            Password = passwordHasher.HashPassword(null!, @event.Password),
        };

        await usersRepository.AddAsync(user);
    }
}