using AsistOff.MES.Multitenancy.Contracts;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Jbl;

namespace AsistOff.MES.Multitenancy.Requests.Queries;

public record GetTenantQuery(Guid Id) : IJblRequest<TenantResponse?>, IAllowAnonymousRequest;
