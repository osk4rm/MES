using AsistOff.MES.Configuration.Application.Features.Shifts.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Shifts.Get;

public record GetShiftRequest(Guid Id) : ITenantRequest<ShiftResponse>;
