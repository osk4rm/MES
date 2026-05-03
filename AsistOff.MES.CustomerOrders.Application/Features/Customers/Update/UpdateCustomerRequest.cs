using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.CustomerOrders.Application.Features.Customers.Update;

public record UpdateCustomerRequest(
    Guid Id,
    string Code,
    string Name,
    string? TaxId,
    string? Email,
    string? Phone,
    string? AddressLine1,
    string? AddressLine2,
    string? PostalCode,
    string? City,
    string? Country,
    bool IsActive,
    string? SyncId) : ITenantRequest;
