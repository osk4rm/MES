using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Requests.Commands.Create;
using AsistOff.MES.Shared.Abstractions.Seeder;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsistOff.MES.Multitenancy.Seeding;

/// <summary>
/// Development-only seeder that provisions tenants (and their tenant-admin users via the existing
/// <c>TenantCreatedEvent</c> listener) by sending the same <see cref="CreateTenantCommand"/> that
/// self-service registration uses. Idempotent: skips tenants whose <c>Name</c> already exists.
/// </summary>
internal sealed class DevTenantSeeder(
    IHostEnvironment environment,
    IOptions<DevTenantSeedOptions> options,
    MultitenancyDbContext db,
    ISender mediator,
    ILogger<DevTenantSeeder> logger)
    : ISeeder
{
    public async Task Seed()
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        var settings = options.Value;
        if (!settings.Enabled || settings.Tenants.Count == 0)
        {
            return;
        }

        foreach (var seed in settings.Tenants)
        {
            if (string.IsNullOrWhiteSpace(seed.Name) ||
                string.IsNullOrWhiteSpace(seed.ContactEmail) ||
                string.IsNullOrWhiteSpace(seed.AdminPassword))
            {
                logger.LogWarning(
                    "Skipping dev tenant seed with incomplete configuration: Name='{Name}'",
                    seed.Name);
                continue;
            }

            var exists = await db.Tenants.AsNoTracking().AnyAsync(t => t.Name == seed.Name);
            if (exists)
            {
                logger.LogInformation("Dev tenant '{Name}' already exists – skipping seed", seed.Name);
                continue;
            }

            logger.LogInformation(
                "Seeding dev tenant '{Name}' with admin '{Email}'",
                seed.Name, seed.ContactEmail);

            var command = new CreateTenantCommand(
                Name: seed.Name,
                DisplayName: string.IsNullOrWhiteSpace(seed.DisplayName) ? seed.Name : seed.DisplayName,
                ContactEmail: seed.ContactEmail,
                Settings: string.Empty,
                Password: seed.AdminPassword,
                ConfirmPassword: seed.AdminPassword);

            try
            {
                await mediator.Send(command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to seed dev tenant '{Name}'", seed.Name);
            }
        }
    }
}
