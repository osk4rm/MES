using System.Text.Json;
using AsistOff.MES.Multitenancy.Contracts.Events;
using AsistOff.MES.Multitenancy.Entity;
using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Shared.Abstractions.Events;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Multitenancy.Requests.Commands.Create;

public class CreateTenantCommandHandler(
    ITenantRepository repo, 
    IGuidProvider guidProvider,
    ILogger<CreateTenantCommandHandler> logger,
    IEventDispatcher eventDispatcher)
    : IRequestHandler<CreateTenantCommand, Guid>
{
    public async Task<Guid> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
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
            await eventDispatcher.PublishAsync(new TenantCreatedEvent(tenant.Id, tenant.ContactEmail, request.Password));
            
            return tenant.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating tenant with name {TenantName}", request.Name);
            throw new RepositoryException("Error creating tenant", ex);
        }
    }
}