using AsistOff.MES.Multitenancy.Contracts;
using AsistOff.MES.Multitenancy.Error;
using AsistOff.MES.Multitenancy.Repositories;
using ErrorOr;
using MediatR;

namespace AsistOff.MES.Multitenancy.Requests.Queries;

public class GetTenantQueryHandler(ITenantRepository tenantRepository)
    : IRequestHandler<GetTenantQuery, ErrorOr<TenantResponse?>>
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
