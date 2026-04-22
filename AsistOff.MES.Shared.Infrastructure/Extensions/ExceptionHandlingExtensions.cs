using AsistOff.MES.Shared.Infrastructure.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Shared.Infrastructure.Extensions;

public static class ExceptionHandlingExtensions
{
    public static IServiceCollection AddExceptionHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        
        return services;
    }
}
