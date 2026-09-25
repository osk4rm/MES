using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.SpcMeasurements.Chart;

/// <summary>
/// Read-only control chart evaluation for one characteristic: limits plus
/// one ordered point per measurement with Western Electric rules 1-4 flags.
/// </summary>
public class GetSpcMeasurementChartRequest : ITenantRequest<SpcMeasurementChartResponse>
{
    public Guid CharacteristicId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
