using AsistOff.MES.Configuration.Application.Features.ProductGroups.Common.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.ProductGroups.Get;

public record GetProductGroupRequest(Guid Id) : ITenantRequest<ProductGroupResponse>;