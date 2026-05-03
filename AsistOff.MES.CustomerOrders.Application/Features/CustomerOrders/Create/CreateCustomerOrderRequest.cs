using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.CustomerOrders.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Create;

public record CreateCustomerOrderLineRequest(
    string? SyncId,
    string? ExternalLineId,
    int LineNumber,
    Guid ProductId,
    Guid? MeasureUnitId,
    decimal OrderedQuantity,
    DateTime? RequestedDeliveryDate,
    decimal? UnitNetPrice,
    decimal? LineNetAmount,
    string? Notes);

public record CreateCustomerOrderRequest(
    string? SyncId,
    string? ExternalSystem,
    string? ExternalOrderId,
    string OrderNumber,
    Guid CustomerId,
    CustomerOrderStatus Status,
    DateTime? OrderDate,
    DateTime? RequestedDeliveryDate,
    DateTime? ConfirmedDeliveryDate,
    string? Currency,
    decimal? TotalNetAmount,
    decimal? TotalGrossAmount,
    string? Notes,
    IReadOnlyCollection<CreateCustomerOrderLineRequest> Lines) : ITenantRequest<CustomerOrderResponse>;
