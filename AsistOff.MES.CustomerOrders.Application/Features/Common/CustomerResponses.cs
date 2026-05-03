using AsistOff.MES.CustomerOrders.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.CustomerOrders.Application.Features.Common;

public record CustomerResponse(
    Guid Id,
    string? SyncId,
    string Code,
    string Name,
    string? TaxId,
    string? Email,
    string? Phone,
    string? AddressLine1,
    string? AddressLine2,
    string? PostalCode,
    string? City,
    string? Country,
    bool IsActive);

public record CustomerShortResponse(Guid Id, string Code, string Name, string? TaxId);

public record ProductionReleaseResponse(
    Guid Id,
    Guid RecipeId,
    Guid RecipeVersionId,
    decimal Quantity,
    DateTime? PlannedStartDate,
    DateTime? PlannedDueDate,
    Guid? ProductionOrderId,
    ProductionReleaseStatus Status,
    string? Notes,
    DateTime CreatedAt);

public record CustomerOrderLineResponse(
    Guid Id,
    string? SyncId,
    string? ExternalLineId,
    int LineNumber,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid? MeasureUnitId,
    string? MeasureUnitCode,
    decimal OrderedQuantity,
    decimal ReleasedQuantity,
    decimal RemainingQuantity,
    DateTime? RequestedDeliveryDate,
    CustomerOrderLineStatus Status,
    decimal? UnitNetPrice,
    decimal? LineNetAmount,
    string? Notes,
    IReadOnlyCollection<ProductionReleaseResponse> ProductionReleases);

public record CustomerOrderResponse(
    Guid Id,
    string? SyncId,
    string? ExternalSystem,
    string? ExternalOrderId,
    string OrderNumber,
    CustomerShortResponse Customer,
    string CustomerNameSnapshot,
    string? CustomerTaxIdSnapshot,
    string? CustomerAddressSnapshot,
    CustomerOrderStatus Status,
    DateTime? OrderDate,
    DateTime? RequestedDeliveryDate,
    DateTime? ConfirmedDeliveryDate,
    string? Currency,
    decimal? TotalNetAmount,
    decimal? TotalGrossAmount,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyCollection<CustomerOrderLineResponse> Lines);

public class PagedCustomersResponse(IReadOnlyCollection<CustomerResponse> items, int totalCount, int? pageSize)
    : PagedResponse<CustomerResponse>(items, totalCount, pageSize);

public class PagedCustomerOrdersResponse(IReadOnlyCollection<CustomerOrderResponse> items, int totalCount, int? pageSize)
    : PagedResponse<CustomerOrderResponse>(items, totalCount, pageSize);
