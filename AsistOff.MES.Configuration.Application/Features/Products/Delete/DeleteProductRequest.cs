using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Products.Delete;

public record DeleteProductRequest(Guid Id) : ITenantRequest;