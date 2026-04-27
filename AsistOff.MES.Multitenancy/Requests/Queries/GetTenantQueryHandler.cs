using AsistOff.MES.Multitenancy.Contracts;
using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Jbl;

namespace AsistOff.MES.Multitenancy.Requests.Queries;

public class GetTenantQueryHandler(ITenantRepository tenantRepository)
    : IJblHandler<GetTenantQuery, TenantResponse?>
{
    public async Task<TenantResponse?> Handle(GetTenantQuery request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(request.Id, cancellationToken);

        if (tenant is null)
        {
            throw new NotFoundException("Tenant", request.Id);
        }

        return new TenantResponse
        (
            Id: tenant.Id,
            Name: tenant.Name,
            DisplayName: tenant.DisplayName,
            ContactEmail: tenant.ContactEmail,
            Settings: new TenantSettingsResponse(tenant.Settings?.Country ?? string.Empty)
        );
    }
}
