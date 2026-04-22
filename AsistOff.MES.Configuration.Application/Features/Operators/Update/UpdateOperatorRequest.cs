using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Operators.Update;

public record UpdateOperatorRequest(
    Guid Id,
    string Identifier,
    string FirstName,
    string LastName,
    decimal RatePerHour,
    Guid? DepartmentId,
    Guid UserId
) : IRequest;
