using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Entity;
using MediatR;

namespace AsistOff.MES.Multitenancy.Requests.Commands.Create;

public class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, Tenant>
{
    private readonly MultitenancyDbContext _db;
    public CreateTenantCommandHandler(MultitenancyDbContext db)
    {
        _db = db;
    }

    public async Task<Tenant> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            DisplayName = request.DisplayName,
            ContactEmail = request.ContactEmail,
            Settings = request.Settings ?? "{}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(cancellationToken);
        return tenant;
    }
}

