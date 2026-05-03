using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Delete;

public record DeleteCustomerOrderRequest(Guid Id) : ITenantRequest;
