using MediatR;

namespace AsistOff.MES.Shared.Abstractions.Jbl;

public interface IJblRequest : IRequest
{
}

public interface IJblRequest<out TResponse> : IRequest<TResponse>
{
}
