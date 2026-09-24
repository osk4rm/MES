using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Test;

/// <summary>
/// Validates the stored endpoint URL shape of a connection. A malformed URL
/// yields <c>ValidationException</c> (400); an unknown or cross-tenant id
/// yields <c>NotFoundException</c> (404).
/// </summary>
public record TestOpcUaConnectionRequest(Guid Id) : ITenantRequest<TestOpcUaConnectionResponse>;
