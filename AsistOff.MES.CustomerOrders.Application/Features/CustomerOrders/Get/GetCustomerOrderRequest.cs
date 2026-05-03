using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Get;

public record GetCustomerOrderRequest(Guid Id) : ITenantRequest<CustomerOrderResponse>;
