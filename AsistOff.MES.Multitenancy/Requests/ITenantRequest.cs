using MediatR;

namespace AsistOff.MES.Multitenancy.Requests;

public interface ITenantRequest : IRequest
{
}

public interface ITenantRequest<out TResponse> : IRequest<TResponse>
{
}