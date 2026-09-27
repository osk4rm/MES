using AsistOff.MES.Attachments.Application;
using AsistOff.MES.Attachments.Infrastructure;
using AsistOff.MES.Attachments.Infrastructure.Storage;
using AsistOff.MES.Shared.Abstractions.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        // Issue #373: log the resolved absolute blob root at startup so
        // operators can verify the attachments_data volume mount. Reuses the
        // same resolution as LocalFileStorage (content-root-relative default,
        // absolute override passes through).
        var environment = app.ApplicationServices.GetRequiredService<IHostEnvironment>();
        var options = app.ApplicationServices.GetRequiredService<IOptions<LocalFileStorageOptions>>();
        var logger = app.ApplicationServices.GetRequiredService<ILogger<AttachmentsModule>>();
        var resolved = LocalFileStorageOptions.ResolveRootPath(options.Value.RootPath, environment.ContentRootPath);
        logger.LogInformation(
            "Attachment blob storage root: {BlobRoot} (configured {Section}:{RootPath})",
            resolved, LocalFileStorageOptions.SectionName, options.Value.RootPath);
    }
}
