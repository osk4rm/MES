using System.Reflection;
using AsistOff.MES.Shared.Abstractions.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Shared.Infrastructure.Validation;

public static class Extensions
{
    internal static void AddValidation(this IServiceCollection services, IList<Assembly> assemblies)
    {
        services.AddTransient(typeof(IRequestValidator<>), typeof(RequestValidator<>));
        
        var validatorTypes = assemblies.SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters)
            .SelectMany(t => t.GetInterfaces(), (t, i) => new { Type = t, Interface = i })
            .Where(x => x.Interface.IsGenericType &&
                        x.Interface.GetGenericTypeDefinition() == typeof(IRequestValidator<>))
            .ToList();

        foreach (var validator in validatorTypes)
        {
            services.AddTransient(validator.Interface, validator.Type);
        }
    }
}