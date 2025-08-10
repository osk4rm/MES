using AsistOff.MES.Multitenancy.Requests;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Update;

public record UpdateWarehouseRequest(Guid Id, string Name) : ITenantRequest<Unit>;
