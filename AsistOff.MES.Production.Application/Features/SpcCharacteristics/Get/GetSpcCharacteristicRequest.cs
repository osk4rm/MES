using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.SpcCharacteristics.Responses;

namespace AsistOff.MES.Production.Application.Features.SpcCharacteristics.Get;

public record GetSpcCharacteristicRequest(Guid Id) : ITenantRequest<SpcCharacteristicResponse>;
