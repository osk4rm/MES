namespace AsistOff.MES.CustomerOrders.Domain.Enums;

public enum CustomerOrderLineStatus
{
    Open = 1,
    PartiallyReleasedToProduction = 2,
    ReleasedToProduction = 3,
    Completed = 4,
    Cancelled = 5
}
