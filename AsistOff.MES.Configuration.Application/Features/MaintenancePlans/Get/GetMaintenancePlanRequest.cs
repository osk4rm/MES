using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Get;

public record GetMaintenancePlanRequest(Guid Id) : ITenantRequest<MaintenancePlanResponse>;
