namespace AsistOff.MES.CustomerOrders.Domain.Enums;

public enum CustomerOrderStatus
{
    Imported = 1,
    Confirmed = 2,
    PartiallyReleasedToProduction = 3,
    ReleasedToProduction = 4,
    Completed = 5,
    Cancelled = 6
}
