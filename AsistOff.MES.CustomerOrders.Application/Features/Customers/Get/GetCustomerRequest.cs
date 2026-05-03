using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.CustomerOrders.Application.Features.Customers.Get;

public record GetCustomerRequest(Guid Id) : ITenantRequest<CustomerResponse>;
