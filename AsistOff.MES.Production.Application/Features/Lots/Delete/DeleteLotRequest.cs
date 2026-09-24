using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Lots.Delete;

public record DeleteLotRequest(Guid Id) : ITenantRequest;
