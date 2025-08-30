namespace AsistOff.MES.Configuration.Application.Features.Products.Common.Responses;

public record MeasureUnitShortResponse(
    Guid Id,
    string Name,
    string Symbol
);