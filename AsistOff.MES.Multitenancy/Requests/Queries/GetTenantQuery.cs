using AsistOff.MES.Multitenancy.Contracts;
using ErrorOr;
using MediatR;

namespace AsistOff.MES.Multitenancy.Requests.Queries;

public record GetTenantQuery(Guid Id) : IRequest<ErrorOr<TenantResponse?>>;

