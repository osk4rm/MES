namespace AsistOff.MES.Production.Domain.Enums;

public enum ProductionOrderStatus : short
{
    Planned = 1,
    Released = 2,
    InProgress = 3,
    Completed = 4,
    Closed = 5
}
