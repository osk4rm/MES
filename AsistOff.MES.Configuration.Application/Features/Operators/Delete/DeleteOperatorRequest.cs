using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Operators.Delete;

public record DeleteOperatorRequest(Guid Id) : IRequest;
