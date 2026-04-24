using AsistOff.MES.Attachments.Domain.Repositories;
using AsistOff.MES.Attachments.Infrastructure.Configurations;
using AsistOff.MES.Attachments.Infrastructure.Repositories;
using AsistOff.MES.Attachments.Infrastructure.Storage;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Attachments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAttachmentsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAttachmentsRepository, AttachmentsRepository>();
        services.AddScoped<IEntityConfigurator, AttachmentsEntityConfigurator>();

        services.Configure<LocalFileStorageOptions>(options =>
        {
            configuration.GetSection(LocalFileStorageOptions.SectionName).Bind(options);
        });
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        return services;
    }
}
