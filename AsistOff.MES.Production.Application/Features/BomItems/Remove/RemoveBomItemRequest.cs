using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.BomItems.Remove;

public record RemoveBomItemRequest(Guid BomItemId) : ITenantRequest;
