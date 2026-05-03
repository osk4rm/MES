using AsistOff.MES.CustomerOrders.Domain.Entities;

namespace AsistOff.MES.CustomerOrders.Application.Features.Common;

internal static class CustomerOrderMappers
{
    public static CustomerResponse Map(Customer customer)
        => new(
            customer.Id,
            customer.SyncId,
            customer.Code,
            customer.Name,
            customer.TaxId,
            customer.Email,
            customer.Phone,
            customer.AddressLine1,
            customer.AddressLine2,
            customer.PostalCode,
            customer.City,
            customer.Country,
            customer.IsActive);

    public static CustomerShortResponse MapShort(Customer customer)
        => new(customer.Id, customer.Code, customer.Name, customer.TaxId);

    public static CustomerOrderResponse Map(CustomerOrder order)
        => new(
            order.Id,
            order.SyncId,
            order.ExternalSystem,
            order.ExternalOrderId,
            order.OrderNumber,
            MapShort(order.Customer),
            order.CustomerNameSnapshot,
            order.CustomerTaxIdSnapshot,
            order.CustomerAddressSnapshot,
            order.Status,
            order.OrderDate,
            order.RequestedDeliveryDate,
            order.ConfirmedDeliveryDate,
            order.Currency,
            order.TotalNetAmount,
            order.TotalGrossAmount,
            order.Notes,
            order.CreatedAt,
            order.UpdatedAt,
            order.Lines.OrderBy(x => x.LineNumber).Select(Map).ToList());

    private static CustomerOrderLineResponse Map(CustomerOrderLine line)
        => new(
            line.Id,
            line.SyncId,
            line.ExternalLineId,
            line.LineNumber,
            line.ProductId,
            line.ProductCodeSnapshot,
            line.ProductNameSnapshot,
            line.MeasureUnitId,
            line.MeasureUnitCodeSnapshot,
            line.OrderedQuantity,
            line.ReleasedQuantity,
            line.RemainingQuantity,
            line.RequestedDeliveryDate,
            line.Status,
            line.UnitNetPrice,
            line.LineNetAmount,
            line.Notes,
            line.ProductionReleases.OrderBy(x => x.CreatedAt).Select(Map).ToList());

    private static ProductionReleaseResponse Map(CustomerOrderLineProductionRelease release)
        => new(
            release.Id,
            release.RecipeId,
            release.RecipeVersionId,
            release.Quantity,
            release.PlannedStartDate,
            release.PlannedDueDate,
            release.ProductionOrderId,
            release.Status,
            release.Notes,
            release.CreatedAt);
}
