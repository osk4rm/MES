namespace AsistOff.MES.Production.Domain.Enums;

public enum ProductionOrderStatus : short
{
    Planned = 1,
    Released = 2,
    InProgress = 3,
    Completed = 4,
    Closed = 5,
    /// <summary>
    /// Suspended via Hold (issue #398): Released/InProgress orders frozen
    /// until Resume restores the pre-hold status. Stored as short, so no
    /// lookup table is needed.
    /// </summary>
    OnHold = 6
}
