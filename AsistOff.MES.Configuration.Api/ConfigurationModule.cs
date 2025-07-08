using AsistOff.MES.Configuration.Application;
using AsistOff.MES.Configuration.Infrastructure;
using AsistOff.MES.Shared.Abstractions.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Configuration.Api;

internal sealed class ConfigurationModule : IModule
{
    public const string BasePath = "configuration";
    public string Name => "Configuration";
    public string Path => BasePath;
    public IEnumerable<string> Policies => ["configuration"];

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddApplication();
        services.AddInfrastructure(configuration);
        // If you have a domain layer with registration, add it here:
        // services.AddDomain();
    }

    public void Use(IApplicationBuilder app)
    {
    }
}