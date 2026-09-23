using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Shifts.Delete;

public record DeleteShiftRequest(Guid Id) : ITenantRequest;
