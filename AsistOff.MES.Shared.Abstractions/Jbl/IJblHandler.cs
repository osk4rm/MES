using MediatR;

namespace AsistOff.MES.Shared.Abstractions.Jbl;

public interface IJblHandler<in TRequest> : IRequestHandler<TRequest>
    where TRequest : IJblRequest
{
}

public interface IJblHandler<in TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    where TRequest : IJblRequest<TResponse>
{
}