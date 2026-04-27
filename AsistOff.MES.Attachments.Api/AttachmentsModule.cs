using AsistOff.MES.Attachments.Application;
using AsistOff.MES.Attachments.Infrastructure;
using AsistOff.MES.Shared.Abstractions.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Attachments.Api;

internal sealed class AttachmentsModule : IModule
{
    public const string BasePath = "attachments";
    public string Name => "Attachments";
    public string Path => BasePath;
    public IEnumerable<string> Policies => ["attachments"];

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAttachmentsApplication();
        services.AddAttachmentsInfrastructure(configuration);
    }

    public void Use(IApplicationBuilder app)
    {
    }
}
