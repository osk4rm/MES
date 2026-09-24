using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Delete;

public record DeleteOpcUaConnectionRequest(Guid Id) : ITenantRequest;
