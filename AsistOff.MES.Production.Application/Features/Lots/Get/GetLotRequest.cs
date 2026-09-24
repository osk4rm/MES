using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Lots.Get;

public record GetLotRequest(Guid Id) : ITenantRequest<LotResponse>;
