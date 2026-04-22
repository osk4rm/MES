using AsistOff.MES.Configuration.Application.Features.ProductGroups.Common.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.ProductGroups.Update;

public record UpdateProductGroupRequest(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    Guid? ParentId,
    string? SyncId
) : ITenantRequest<ProductGroupResponse>;
