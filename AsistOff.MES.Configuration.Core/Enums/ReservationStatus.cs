namespace AsistOff.MES.Configuration.Domain.Enums;

/// <summary>
/// Lifecycle of a soft material reservation (issue #291). Reservations are
/// created on Production Order release and relieved by RW confirmation
/// postings; closing the order closes whatever is left.
/// </summary>
public enum ReservationStatus : short
{
    Active = 1,
    PartiallyRelieved = 2,
    Closed = 3
}
