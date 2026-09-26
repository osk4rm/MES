using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.ShiftHandovers.Get;

public record GetShiftHandoverByIdRequest(Guid Id) : ITenantRequest<ShiftHandoverResponse>;
