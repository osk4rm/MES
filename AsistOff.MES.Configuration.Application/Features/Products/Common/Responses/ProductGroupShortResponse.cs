namespace AsistOff.MES.Configuration.Application.Features.Products.Common.Responses;

public record ProductGroupShortResponse(
    Guid Id,
    string Code,
    string Name
);