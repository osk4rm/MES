using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.SpcCharacteristics.Delete;

public record DeleteSpcCharacteristicRequest(Guid Id) : ITenantRequest;
