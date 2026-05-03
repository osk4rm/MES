using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.CustomerOrders.Domain.Entities;
using AsistOff.MES.CustomerOrders.Domain.Enums;
using AsistOff.MES.CustomerOrders.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.ReleaseToProduction;

internal sealed class CreateProductionReleaseRequestHandler(
    ICustomerOrderLinesRepository linesRepository,
    ICustomerOrdersRepository ordersRepository,
    IRecipesRepository recipesRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateProductionReleaseRequest, CustomerOrderResponse>
{
    public async Task<CustomerOrderResponse> Handle(CreateProductionReleaseRequest request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0) throw new ValidationException(nameof(request.Quantity), "Quantity must be greater than zero");
        var line = await linesRepository.GetWithOrderAndReleasesAsync(request.CustomerOrderLineId, cancellationToken)
            ?? throw new NotFoundException($"Customer order line '{request.CustomerOrderLineId}' was not found.");
        if (line.Status is CustomerOrderLineStatus.Cancelled or CustomerOrderLineStatus.Completed)
            throw new ValidationException(nameof(request.CustomerOrderLineId), "Cannot release a closed or cancelled order line to production");
        if (request.Quantity > line.RemainingQuantity)
            throw new ValidationException(nameof(request.Quantity), "Quantity exceeds remaining order line quantity");

        var recipe = await recipesRepository.GetWithVersionsAsync(request.RecipeId, cancellationToken)
            ?? throw new NotFoundException($"Recipe '{request.RecipeId}' was not found.");
        if (!recipe.IsActive) throw new ValidationException(nameof(request.RecipeId), "Recipe is inactive");
        if (recipe.PrimaryProductId != line.ProductId) throw new ValidationException(nameof(request.RecipeId), "Recipe does not match the order line product");

        var recipeVersionId = request.RecipeVersionId ?? recipe.CurrentVersionId;
        var version = recipe.Versions.FirstOrDefault(x => x.Id == recipeVersionId)
            ?? throw new ValidationException(nameof(request.RecipeVersionId), "Recipe has no selected version");
        if (version.Status != RecipeVersionStatus.Released || recipe.CurrentVersionId != version.Id)
            throw new ValidationException(nameof(request.RecipeVersionId), "Only the current released recipe version can be used");

        var release = new CustomerOrderLineProductionRelease
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            CustomerOrderLineId = line.Id,
            RecipeId = recipe.Id,
            RecipeVersionId = version.Id,
            Quantity = request.Quantity,
            PlannedStartDate = request.PlannedStartDate,
            PlannedDueDate = request.PlannedDueDate ?? line.RequestedDeliveryDate ?? line.CustomerOrder.RequestedDeliveryDate,
            Notes = request.Notes,
            Status = ProductionReleaseStatus.Planned
        };

        line.ProductionReleases.Add(release);
        line.ReleasedQuantity += request.Quantity;
        CustomerOrderHelpers.RefreshReleaseStatuses(line.CustomerOrder);
        await linesRepository.UpdateAsync(line, cancellationToken);
        return CustomerOrderMappers.Map((await ordersRepository.GetWithLinesAsync(line.CustomerOrderId, cancellationToken))!);
    }
}
