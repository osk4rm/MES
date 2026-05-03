using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Lines;

public record DeleteCustomerOrderLineRequest(Guid Id) : ITenantRequest<CustomerOrderResponse>;
