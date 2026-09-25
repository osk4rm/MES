using AsistOff.MES.Attachments.Application.Features.Common;
using AsistOff.MES.Attachments.Application.Features.Upload;
using AsistOff.MES.Attachments.Domain.Repositories;
using AsistOff.MES.Attachments.Infrastructure.Configurations;
using AsistOff.MES.Attachments.Infrastructure.Repositories;
using AsistOff.MES.Attachments.Infrastructure.Storage;
using AsistOff.MES.Attachments.Infrastructure.Verification;
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
        services.AddScoped<IAttachmentOwnerVerifier, EfAttachmentOwnerVerifier>();

        services.Configure<AttachmentUploadOptions>(options =>
        {
            configuration.GetSection(AttachmentUploadOptions.SectionName).Bind(options);
        });

        // Configuration binding replaces the HashSet instances (losing the
        // case-insensitive comparer) without normalizing values; re-normalize
        // so a custom Attachments:Upload section keeps default match semantics.
        services.PostConfigure<AttachmentUploadOptions>(options => options.Normalize());

        services.Configure<LocalFileStorageOptions>(options =>
        {
            configuration.GetSection(LocalFileStorageOptions.SectionName).Bind(options);
        });
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        return services;
    }
}
