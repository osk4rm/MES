using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.CustomerOrders.Application.Features.Customers.Create;

public record CreateCustomerRequest(
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
    string? SyncId) : ITenantRequest<CustomerResponse>;
