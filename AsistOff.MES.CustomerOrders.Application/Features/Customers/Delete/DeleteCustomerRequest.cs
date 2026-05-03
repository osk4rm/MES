using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.CustomerOrders.Application.Features.Customers.Delete;

public record DeleteCustomerRequest(Guid Id) : ITenantRequest;
