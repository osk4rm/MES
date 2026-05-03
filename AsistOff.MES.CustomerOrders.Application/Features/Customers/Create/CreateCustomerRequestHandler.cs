using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.CustomerOrders.Domain.Entities;
using AsistOff.MES.CustomerOrders.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.CustomerOrders.Application.Features.Customers.Create;

internal sealed class CreateCustomerRequestHandler(ICustomersRepository repository, IGuidProvider guidProvider, ITenantContext tenantContext)
    : IRequestHandler<CreateCustomerRequest, CustomerResponse>
{
    public async Task<CustomerResponse> Handle(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code)) throw new ValidationException(nameof(request.Code), "Code is required");
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ValidationException(nameof(request.Name), "Name is required");
        if (await repository.CodeExistsAsync(request.Code, null, cancellationToken)) throw new ConflictException($"Customer with code '{request.Code}' already exists.");

        var customer = new Customer
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            Code = request.Code,
            Name = request.Name,
            TaxId = request.TaxId,
            Email = request.Email,
            Phone = request.Phone,
            AddressLine1 = request.AddressLine1,
            AddressLine2 = request.AddressLine2,
            PostalCode = request.PostalCode,
            City = request.City,
            Country = request.Country,
            IsActive = request.IsActive,
            SyncId = request.SyncId
        };

        await repository.AddAsync(customer, cancellationToken);
        return CustomerOrderMappers.Map(customer);
    }
}
