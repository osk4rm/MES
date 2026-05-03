using AsistOff.MES.CustomerOrders.Domain.Entities;
using AsistOff.MES.CustomerOrders.Domain.Enums;

namespace AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders;

internal static class CustomerOrderHelpers
{
    public static string BuildCustomerAddressSnapshot(Customer customer)
        => string.Join(", ", new[] { customer.AddressLine1, customer.AddressLine2, customer.PostalCode, customer.City, customer.Country }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

    public static void RefreshReleaseStatuses(CustomerOrder order)
    {
        foreach (var line in order.Lines.Where(x => x.Status is not CustomerOrderLineStatus.Cancelled and not CustomerOrderLineStatus.Completed))
        {
            line.Status = line.ReleasedQuantity <= 0
                ? CustomerOrderLineStatus.Open
                : line.ReleasedQuantity >= line.OrderedQuantity
                    ? CustomerOrderLineStatus.ReleasedToProduction
                    : CustomerOrderLineStatus.PartiallyReleasedToProduction;
        }

        if (order.Status is CustomerOrderStatus.Cancelled or CustomerOrderStatus.Completed)
        {
            return;
        }

        var activeLines = order.Lines.Where(x => x.Status != CustomerOrderLineStatus.Cancelled).ToList();
        if (activeLines.Count == 0)
        {
            return;
        }

        if (activeLines.All(x => x.Status == CustomerOrderLineStatus.Completed))
        {
            order.Status = CustomerOrderStatus.Completed;
        }
        else if (activeLines.All(x => x.Status is CustomerOrderLineStatus.ReleasedToProduction or CustomerOrderLineStatus.Completed))
        {
            order.Status = CustomerOrderStatus.ReleasedToProduction;
        }
        else if (activeLines.Any(x => x.Status == CustomerOrderLineStatus.PartiallyReleasedToProduction || x.Status == CustomerOrderLineStatus.ReleasedToProduction))
        {
            order.Status = CustomerOrderStatus.PartiallyReleasedToProduction;
        }
    }
}
