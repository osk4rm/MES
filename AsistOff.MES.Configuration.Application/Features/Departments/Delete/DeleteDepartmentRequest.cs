using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.Departments.Delete;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record DeleteDepartmentRequest(Guid Id) : ITenantRequest;
