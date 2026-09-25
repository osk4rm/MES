using AsistOff.MES.Configuration.Application.Features.Warehouses.Common;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Create;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record CreateWarehouseRequest(string Name, string? SyncId) : ITenantRequest<WarehouseResult>;
