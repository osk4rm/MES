using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.Lots.ChangeStatus;

public record ChangeLotStatusRequest(Guid Id, LotStatus Status) : ITenantRequest;
