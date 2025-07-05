using AsistOff.MES.Gateway.Errors;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace AsistOff.MES.Gateway;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddHttpContextAccessor();

        services.AddSingleton<ProblemDetailsFactory, CustomProblemDetailsFactory>();

        return services;
    }
}