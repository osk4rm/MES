using AsistOff.MES.Configuration.Application.Features.Products.Common.Responses;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;

namespace AsistOff.MES.Configuration.Application.Features.Products.Create;

[RequirePermission("configuration.write")]
public record CreateProductRequest(
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