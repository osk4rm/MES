namespace AsistOff.MES.Production.Domain.Enums;

public enum LotStatus : short
{
    Available = 1,
    OnHold = 2,
    Consumed = 3,
    Scrapped = 4,
    Expired = 5
}
