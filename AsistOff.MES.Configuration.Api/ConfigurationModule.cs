using AsistOff.MES.Shared.Abstractions.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Configuration.Api;

internal sealed class ConfigurationModule : IModule
{
    public const string BasePath = "configuration";
    public string Name => "Configuration";
    public string Path => BasePath;
    public IEnumerable<string> Policies => ["configuration"];

    public void Register(IServiceCollection services)
    {
    }

    public void Use(IApplicationBuilder app)
    {
    }
}