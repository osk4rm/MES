using System.Text.Json;
using AsistOff.MES.Multitenancy.Entity;
using AsistOff.MES.Multitenancy.Error;
using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Shared.Abstractions.Providers;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Multitenancy.Requests.Commands.Create;

public class CreateTenantCommandHandler(
    ITenantRepository repo, 
    IGuidProvider guidProvider,
    ILogger<CreateTenantCommandHandler> logger)
    : IRequestHandler<CreateTenantCommand, ErrorOr<Guid>>
{
    public async Task<ErrorOr<Guid>> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = new Tenant
        {
            Id = guidProvider.NewGuid(),
            Name = request.Name,
            DisplayName = request.DisplayName,
            ContactEmail = request.ContactEmail,
            Settings = string.IsNullOrWhiteSpace(request.Settings)
                ? new TenantSettings()
                : JsonSerializer.Deserialize<TenantSettings>(request.Settings) ?? new TenantSettings(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        try
        {
            await repo.CreateAsync(tenant, cancellationToken);
            return tenant.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating tenant with name {TenantName}", request.Name);
            return Errors.Tenants.CreateFailed;
        }
    }
}