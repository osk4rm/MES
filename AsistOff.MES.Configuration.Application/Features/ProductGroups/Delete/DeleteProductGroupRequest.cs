using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.ProductGroups.Delete;

public record DeleteProductGroupRequest(Guid Id) : ITenantRequest;
