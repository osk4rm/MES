using AsistOff.MES.Users.Core.Repositories;
using AsistOff.MES.Users.Infrastructure.Configurations;
using AsistOff.MES.Users.Infrastructure.Repositories;
using AsistOff.MES.Shared.Abstractions.DAL;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Users.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddUsersInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<IEntityConfigurator, UsersEntityConfigurator>();
        
        return services;
    }
}
