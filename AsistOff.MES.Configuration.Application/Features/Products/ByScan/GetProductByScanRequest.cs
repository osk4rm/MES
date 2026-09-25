using AsistOff.MES.Configuration.Application.Features.Products.Common.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Products.ByScan;

public record GetProductByScanRequest(string? Value) : ITenantRequest<ProductResponse>;
