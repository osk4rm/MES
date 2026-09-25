using AsistOff.MES.Configuration.Application.Features.ProductGroups.Common.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.ProductGroups.Create;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record CreateProductGroupRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    Guid? ParentId,
    string? SyncId
) : ITenantRequest<ProductGroupResponse>;
