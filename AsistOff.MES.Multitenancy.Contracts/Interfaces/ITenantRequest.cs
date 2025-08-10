using AsistOff.MES.Shared.Abstractions.Jbl;

namespace AsistOff.MES.Multitenancy.Contracts.Interfaces;

public interface ITenantRequest : IJblRequest
{
}

public interface ITenantRequest<out TResponse> : IJblRequest<TResponse>
{
}