using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.CustomerOrders.Domain.Entities;
using AsistOff.MES.CustomerOrders.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Lines;

internal sealed class AddCustomerOrderLineRequestHandler(
    ICustomerOrdersRepository ordersRepository,
    IProductsRepository productsRepository,
    IMeasureUnitsRepository measureUnitsRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<AddCustomerOrderLineRequest, CustomerOrderResponse>
{
    public async Task<CustomerOrderResponse> Handle(AddCustomerOrderLineRequest request, CancellationToken cancellationToken)
    {
        var order = await ordersRepository.GetWithLinesAsync(request.CustomerOrderId, cancellationToken)
            ?? throw new NotFoundException($"Customer order '{request.CustomerOrderId}' was not found.");
        order.Lines.Add(await CreateLineAsync(request, order.Id, cancellationToken));
        await ordersRepository.UpdateAsync(order, cancellationToken);
        return CustomerOrderMappers.Map((await ordersRepository.GetWithLinesAsync(order.Id, cancellationToken))!);
    }

    private async Task<CustomerOrderLine> CreateLineAsync(AddCustomerOrderLineRequest request, Guid orderId, CancellationToken cancellationToken)
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

internal sealed class UpdateCustomerOrderLineRequestHandler(
    ICustomerOrderLinesRepository linesRepository,
    ICustomerOrdersRepository ordersRepository,
    IProductsRepository productsRepository,
    IMeasureUnitsRepository measureUnitsRepository)
    : IRequestHandler<UpdateCustomerOrderLineRequest, CustomerOrderResponse>
{
    public async Task<CustomerOrderResponse> Handle(UpdateCustomerOrderLineRequest request, CancellationToken cancellationToken)
    {
        if (request.OrderedQuantity <= 0) throw new ValidationException(nameof(request.OrderedQuantity), "Ordered quantity must be greater than zero");
        var line = await linesRepository.GetWithOrderAndReleasesAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Customer order line '{request.Id}' was not found.");
        if (request.OrderedQuantity < line.ReleasedQuantity) throw new ValidationException(nameof(request.OrderedQuantity), "Ordered quantity cannot be lower than already released quantity");
        var product = await productsRepository.GetAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException($"Product '{request.ProductId}' was not found.");
        var measureUnitCode = request.MeasureUnitId.HasValue
            ? (await measureUnitsRepository.GetAsync(request.MeasureUnitId.Value, cancellationToken))?.Symbol ?? throw new NotFoundException($"Measure unit '{request.MeasureUnitId}' was not found.")
            : null;

        line.SyncId = request.SyncId;
        line.ExternalLineId = request.ExternalLineId;
        line.LineNumber = request.LineNumber;
        line.ProductId = product.Id;
        line.ProductCodeSnapshot = product.Code;
        line.ProductNameSnapshot = product.Name;
        line.MeasureUnitId = request.MeasureUnitId;
        line.MeasureUnitCodeSnapshot = measureUnitCode;
        line.OrderedQuantity = request.OrderedQuantity;
        line.RequestedDeliveryDate = request.RequestedDeliveryDate;
        line.Status = request.Status;
        line.UnitNetPrice = request.UnitNetPrice;
        line.LineNetAmount = request.LineNetAmount ?? (request.UnitNetPrice.HasValue ? request.UnitNetPrice.Value * request.OrderedQuantity : null);
        line.Notes = request.Notes;
        CustomerOrderHelpers.RefreshReleaseStatuses(line.CustomerOrder);
        await linesRepository.UpdateAsync(line, cancellationToken);
        return CustomerOrderMappers.Map((await ordersRepository.GetWithLinesAsync(line.CustomerOrderId, cancellationToken))!);
    }
}

internal sealed class DeleteCustomerOrderLineRequestHandler(ICustomerOrderLinesRepository linesRepository, ICustomerOrdersRepository ordersRepository)
    : IRequestHandler<DeleteCustomerOrderLineRequest, CustomerOrderResponse>
{
    public async Task<CustomerOrderResponse> Handle(DeleteCustomerOrderLineRequest request, CancellationToken cancellationToken)
    {
        var line = await linesRepository.GetWithOrderAndReleasesAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Customer order line '{request.Id}' was not found.");
        if (line.ProductionReleases.Count > 0) throw new ValidationException(nameof(request.Id), "Cannot delete a line already released to production");
        var orderId = line.CustomerOrderId;
        await linesRepository.DeleteAsync(line.Id, cancellationToken);
        return CustomerOrderMappers.Map((await ordersRepository.GetWithLinesAsync(orderId, cancellationToken))!);
    }
}
