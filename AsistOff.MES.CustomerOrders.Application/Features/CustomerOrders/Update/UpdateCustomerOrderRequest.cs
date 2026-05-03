using AsistOff.MES.CustomerOrders.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Update;

public record UpdateCustomerOrderRequest(
    Guid Id,
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
    string? Notes) : ITenantRequest;
