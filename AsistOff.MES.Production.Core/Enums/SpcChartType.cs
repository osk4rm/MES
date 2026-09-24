namespace AsistOff.MES.Production.Domain.Enums;

/// <summary>
/// The control chart type used to monitor an SPC characteristic.
/// </summary>
public enum SpcChartType : short
{
    XbarR = 1,
    XbarS = 2,
    XmR = 3,
    PChart = 4,
    CChart = 5
}
