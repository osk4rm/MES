using AsistOff.MES.Users.Core.Repositories;
using AsistOff.MES.Users.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Users.Infrastructure;

public static class Extensions
{
    public static void AddInfrastructure(this IServiceCollection services)
        => services
            .AddScoped<IUsersRepository, UsersRepository>();
}