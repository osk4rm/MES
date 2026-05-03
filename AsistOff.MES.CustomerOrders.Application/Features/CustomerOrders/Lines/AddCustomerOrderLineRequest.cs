using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Lines;

public record AddCustomerOrderLineRequest(
    Guid CustomerOrderId,
    string? SyncId,
    string? ExternalLineId,
    int LineNumber,
    Guid ProductId,
    Guid? MeasureUnitId,
    decimal OrderedQuantity,
    DateTime? RequestedDeliveryDate,
    decimal? UnitNetPrice,
    decimal? LineNetAmount,
    string? Notes) : ITenantRequest<CustomerOrderResponse>;
