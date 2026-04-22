using AsistOff.MES.Configuration.Application.Features.MeasureUnits.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.MeasureUnits.Get;

public record GetMeasureUnitRequest(Guid Id) : ITenantRequest<MeasureUnitResponse>;
