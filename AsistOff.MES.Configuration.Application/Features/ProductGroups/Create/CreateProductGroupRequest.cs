using AsistOff.MES.Configuration.Application.Features.ProductGroups.Common.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.ProductGroups.Create;

public record CreateProductGroupRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    Guid? ParentId,
    string? SyncId
) : ITenantRequest<ProductGroupResponse>;
