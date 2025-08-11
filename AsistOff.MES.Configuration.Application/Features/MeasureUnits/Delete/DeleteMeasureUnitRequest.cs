using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.MeasureUnits.Delete;

public record DeleteMeasureUnitRequest(Guid Id) : ITenantRequest;
