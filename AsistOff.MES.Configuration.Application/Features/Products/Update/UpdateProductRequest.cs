using AsistOff.MES.Configuration.Application.Features.Products.Common.Responses;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Products.Update;

public record UpdateProductRequest(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? Ean,
    string? Barcode,
    ScanBy ScanBy,
    bool IsActive,
    Guid? ProductGroupId,
    string? SyncId
) : ITenantRequest<ProductResponse>;