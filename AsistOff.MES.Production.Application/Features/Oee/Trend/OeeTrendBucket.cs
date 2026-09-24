namespace AsistOff.MES.Production.Application.Features.Oee.Trend;

/// <summary>
/// Trend bucketing granularity. Buckets are calendar-aligned in UTC:
/// <see cref="Day"/> splits the window at UTC midnights, <see cref="Week"/>
/// splits it at Monday 00:00 UTC boundaries.
/// </summary>
public enum OeeTrendBucket
{
    Day = 0,
    Week = 1
}
