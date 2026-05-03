using AsistOff.MES.CustomerOrders.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.CustomerOrders.Application.Features.Customers.Update;

internal sealed class UpdateCustomerRequestHandler(ICustomersRepository repository) : IRequestHandler<UpdateCustomerRequest>
{
    public async Task Handle(UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code)) throw new ValidationException(nameof(request.Code), "Code is required");
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ValidationException(nameof(request.Name), "Name is required");
        if (await repository.CodeExistsAsync(request.Code, request.Id, cancellationToken)) throw new ConflictException($"Customer with code '{request.Code}' already exists.");

        var customer = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Customer '{request.Id}' was not found.");
        customer.Code = request.Code;
        customer.Name = request.Name;
        customer.TaxId = request.TaxId;
        customer.Email = request.Email;
        customer.Phone = request.Phone;
        customer.AddressLine1 = request.AddressLine1;
        customer.AddressLine2 = request.AddressLine2;
        customer.PostalCode = request.PostalCode;
        customer.City = request.City;
        customer.Country = request.Country;
        customer.IsActive = request.IsActive;
        customer.SyncId = request.SyncId;
        await repository.UpdateAsync(customer, cancellationToken);
    }
}
