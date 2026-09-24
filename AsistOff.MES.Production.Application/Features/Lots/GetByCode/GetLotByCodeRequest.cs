using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Lots.GetByCode;

public record GetLotByCodeRequest(string Code) : ITenantRequest<LotResponse>;
