namespace AsistOff.MES.Production.Domain.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.DowntimeEvent"/>. Derived from
/// <c>EndedAt</c> (null = open), never persisted.
/// </summary>
public enum DowntimeEventStatus : short
{
    Open = 1,
    Closed = 2
}
