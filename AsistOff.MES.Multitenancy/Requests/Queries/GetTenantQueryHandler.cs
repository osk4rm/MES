using AsistOff.MES.Multitenancy.Contracts;
using AsistOff.MES.Multitenancy.Error;
using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Shared.Abstractions.Jbl;
using ErrorOr;

namespace AsistOff.MES.Multitenancy.Requests.Queries;

public class GetTenantQueryHandler(ITenantRepository tenantRepository)
    : IJblHandler<GetTenantQuery, ErrorOr<TenantResponse?>>
{
    public async Task<ErrorOr<TenantResponse?>> Handle(GetTenantQuery request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(request.Id, cancellationToken);

        if (tenant is null)
        {
            return Errors.Tenants.NotFound;
        }

        return new TenantResponse
        (
            Id: tenant.Id,
            Name: tenant.Name,
            DisplayName: tenant.DisplayName,
            ContactEmail: tenant.ContactEmail,
            Settings: new TenantSettingsResponse()
        );
    }
}
