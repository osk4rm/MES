using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.CustomerOrders.Domain.Entities;
using AsistOff.MES.CustomerOrders.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Create;

internal sealed class CreateCustomerOrderRequestHandler(
    ICustomerOrdersRepository ordersRepository,
    ICustomersRepository customersRepository,
    IProductsRepository productsRepository,
    IMeasureUnitsRepository measureUnitsRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateCustomerOrderRequest, CustomerOrderResponse>
{
    public async Task<CustomerOrderResponse> Handle(CreateCustomerOrderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OrderNumber)) throw new ValidationException(nameof(request.OrderNumber), "Order number is required");
        if (await ordersRepository.OrderNumberExistsAsync(request.OrderNumber, null, cancellationToken)) throw new ConflictException($"Customer order '{request.OrderNumber}' already exists.");
        if (!string.IsNullOrWhiteSpace(request.ExternalSystem) && !string.IsNullOrWhiteSpace(request.ExternalOrderId) && await ordersRepository.ExternalOrderExistsAsync(request.ExternalSystem, request.ExternalOrderId, null, cancellationToken)) throw new ConflictException($"External customer order '{request.ExternalSystem}/{request.ExternalOrderId}' already exists.");

        var customer = await customersRepository.GetAsync(request.CustomerId, cancellationToken)
            ?? throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        var order = new CustomerOrder
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            SyncId = request.SyncId,
            ExternalSystem = request.ExternalSystem,
            ExternalOrderId = request.ExternalOrderId,
            OrderNumber = request.OrderNumber,
            CustomerId = customer.Id,
            CustomerCodeSnapshot = customer.Code,
            CustomerNameSnapshot = customer.Name,
            CustomerTaxIdSnapshot = customer.TaxId,
            CustomerAddressSnapshot = CustomerOrderHelpers.BuildCustomerAddressSnapshot(customer),
            Status = request.Status,
            OrderDate = request.OrderDate,
            RequestedDeliveryDate = request.RequestedDeliveryDate,
            ConfirmedDeliveryDate = request.ConfirmedDeliveryDate,
            Currency = request.Currency,
            TotalNetAmount = request.TotalNetAmount,
            TotalGrossAmount = request.TotalGrossAmount,
            Notes = request.Notes
        };

        foreach (var lineRequest in request.Lines.OrderBy(x => x.LineNumber))
        {
            order.Lines.Add(await CreateLineAsync(lineRequest, order.Id, cancellationToken));
        }

        await ordersRepository.AddAsync(order, cancellationToken);
        var full = await ordersRepository.GetWithLinesAsync(order.Id, cancellationToken);
        return CustomerOrderMappers.Map(full!);
    }

    private async Task<CustomerOrderLine> CreateLineAsync(CreateCustomerOrderLineRequest request, Guid orderId, CancellationToken cancellationToken)
    {
        if (request.OrderedQuantity <= 0) throw new ValidationException(nameof(request.OrderedQuantity), "Ordered quantity must be greater than zero");
        var product = await productsRepository.GetAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException($"Product '{request.ProductId}' was not found.");
        var measureUnitCode = request.MeasureUnitId.HasValue
            ? (await measureUnitsRepository.GetAsync(request.MeasureUnitId.Value, cancellationToken))?.Symbol ?? throw new NotFoundException($"Measure unit '{request.MeasureUnitId}' was not found.")
            : null;

        return new CustomerOrderLine
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            CustomerOrderId = orderId,
            SyncId = request.SyncId,
            ExternalLineId = request.ExternalLineId,
            LineNumber = request.LineNumber,
            ProductId = product.Id,
            ProductCodeSnapshot = product.Code,
            ProductNameSnapshot = product.Name,
            MeasureUnitId = request.MeasureUnitId,
            MeasureUnitCodeSnapshot = measureUnitCode,
            OrderedQuantity = request.OrderedQuantity,
            RequestedDeliveryDate = request.RequestedDeliveryDate,
            UnitNetPrice = request.UnitNetPrice,
            LineNetAmount = request.LineNetAmount ?? (request.UnitNetPrice.HasValue ? request.UnitNetPrice.Value * request.OrderedQuantity : null),
            Notes = request.Notes
        };
    }
}
