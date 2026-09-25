using AsistOff.MES.Configuration.Application.Features.Operators.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.Operators.Create;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record CreateOperatorRequest(
    string Identifier,
    string FirstName,
    string LastName,
    decimal RatePerHour,
    Guid? DepartmentId,
    Guid UserId
) : ITenantRequest<OperatorResponse>;
