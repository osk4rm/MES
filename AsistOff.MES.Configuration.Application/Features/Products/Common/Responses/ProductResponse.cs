using AsistOff.MES.Configuration.Domain.Enums;

namespace AsistOff.MES.Configuration.Application.Features.Products.Common.Responses;

public record ProductResponse(
    Guid Id,
    string? SyncId,
    string Code,
    string Name,
    string? Description,
    string? Ean,
    string? Barcode,
    ScanBy ScanBy,
    bool IsActive,
    ProductGroupShortResponse? Group,
    MeasureUnitShortResponse? DefaultMeasureUnit
);