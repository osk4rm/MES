using AsistOff.MES.Multitenancy.Entity;
using MediatR;

namespace AsistOff.MES.Multitenancy.Requests.Queries;

public class GetTenantQuery : IRequest<Tenant?>
{
    public Guid Id { get; }
    public GetTenantQuery(Guid id) => Id = id;
}

