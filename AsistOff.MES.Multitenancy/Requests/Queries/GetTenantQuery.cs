using AsistOff.MES.Multitenancy.Contracts;
using AsistOff.MES.Shared.Abstractions.Jbl;
using ErrorOr;

namespace AsistOff.MES.Multitenancy.Requests.Queries;

public record GetTenantQuery(Guid Id) : IJblRequest<ErrorOr<TenantResponse?>>;

