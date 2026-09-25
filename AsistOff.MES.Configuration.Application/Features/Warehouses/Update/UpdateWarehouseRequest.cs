using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using MediatR;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Update;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record UpdateWarehouseRequest(Guid Id, string Name) : ITenantRequest<Unit>;
