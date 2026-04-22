using AsistOff.MES.Configuration.Application.Features.Products.Common.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Products.Get;

public record GetProductRequest(Guid Id) : ITenantRequest<ProductResponse>;