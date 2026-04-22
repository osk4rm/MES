using AsistOff.MES.Configuration.Application.Features.Operators.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Operators.Create;

public record CreateOperatorRequest(
    string Identifier,
    string FirstName,
    string LastName,
    decimal RatePerHour,
    Guid? DepartmentId,
    Guid UserId
) : ITenantRequest<OperatorResponse>;
