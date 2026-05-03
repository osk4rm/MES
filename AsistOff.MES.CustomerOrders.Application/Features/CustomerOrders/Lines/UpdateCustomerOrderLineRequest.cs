using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.CustomerOrders.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Lines;

public record UpdateCustomerOrderLineRequest(
    Guid Id,
    string? SyncId,
    string? ExternalLineId,
    int LineNumber,
    Guid ProductId,
    Guid? MeasureUnitId,
    decimal OrderedQuantity,
    DateTime? RequestedDeliveryDate,
    CustomerOrderLineStatus Status,
    decimal? UnitNetPrice,
    decimal? LineNetAmount,
    string? Notes) : ITenantRequest<CustomerOrderResponse>;
