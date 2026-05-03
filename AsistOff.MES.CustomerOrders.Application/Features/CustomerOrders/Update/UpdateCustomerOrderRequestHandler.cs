using AsistOff.MES.CustomerOrders.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Update;

internal sealed class UpdateCustomerOrderRequestHandler(ICustomerOrdersRepository ordersRepository, ICustomersRepository customersRepository)
    : IRequestHandler<UpdateCustomerOrderRequest>
{
    public async Task Handle(UpdateCustomerOrderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OrderNumber)) throw new ValidationException(nameof(request.OrderNumber), "Order number is required");
        if (await ordersRepository.OrderNumberExistsAsync(request.OrderNumber, request.Id, cancellationToken)) throw new ConflictException($"Customer order '{request.OrderNumber}' already exists.");
        if (!string.IsNullOrWhiteSpace(request.ExternalSystem) && !string.IsNullOrWhiteSpace(request.ExternalOrderId) && await ordersRepository.ExternalOrderExistsAsync(request.ExternalSystem, request.ExternalOrderId, request.Id, cancellationToken)) throw new ConflictException($"External customer order '{request.ExternalSystem}/{request.ExternalOrderId}' already exists.");

        var order = await ordersRepository.GetWithLinesAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Customer order '{request.Id}' was not found.");
        var customer = await customersRepository.GetAsync(request.CustomerId, cancellationToken)
            ?? throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        order.SyncId = request.SyncId;
        order.ExternalSystem = request.ExternalSystem;
        order.ExternalOrderId = request.ExternalOrderId;
        order.OrderNumber = request.OrderNumber;
        order.CustomerId = customer.Id;
        order.CustomerCodeSnapshot = customer.Code;
        order.CustomerNameSnapshot = customer.Name;
        order.CustomerTaxIdSnapshot = customer.TaxId;
        order.CustomerAddressSnapshot = CustomerOrderHelpers.BuildCustomerAddressSnapshot(customer);
        order.Status = request.Status;
        order.OrderDate = request.OrderDate;
        order.RequestedDeliveryDate = request.RequestedDeliveryDate;
        order.ConfirmedDeliveryDate = request.ConfirmedDeliveryDate;
        order.Currency = request.Currency;
        order.TotalNetAmount = request.TotalNetAmount;
        order.TotalGrossAmount = request.TotalGrossAmount;
        order.Notes = request.Notes;

        await ordersRepository.UpdateAsync(order, cancellationToken);
    }
}
