using AsistOff.MES.Production.Application;
using AsistOff.MES.Production.Infrastructure;
using AsistOff.MES.Shared.Abstractions.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Production.Api;

internal sealed class ProductionModule : IModule
{
    public const string BasePath = "production";
    public string Name => "Production";
    public string Path => BasePath;
    public IEnumerable<string> Policies => ["production"];

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddProductionApplication();
        services.AddProductionInfrastructure(configuration);
    }

    public void Use(IApplicationBuilder app)
    {
    }
}
