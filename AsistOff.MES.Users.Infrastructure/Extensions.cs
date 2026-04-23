using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Users.Core.Repositories;
using AsistOff.MES.Users.Infrastructure.Configurations;
using AsistOff.MES.Users.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Users.Infrastructure;

public static class Extensions
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<IEntityConfigurator, UsersEntityConfigurator>();
    }
}