using AsistOff.MES.Configuration.Application.Features.Departments.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.Departments.Create;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record CreateDepartmentRequest(
    string Code,
    string Name
) : ITenantRequest<DepartmentResponse>;
