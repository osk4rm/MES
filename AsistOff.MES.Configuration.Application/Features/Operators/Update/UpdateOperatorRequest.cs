using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.Operators.Update;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record UpdateOperatorRequest(
    Guid Id,
    string Identifier,
    string FirstName,
    string LastName,
    decimal RatePerHour,
    Guid? DepartmentId,
    Guid UserId
) : ITenantRequest;
