using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Configuration.Application.Features.StockMovements.StockOnHand;

public class GetStockOnHandRequestValidator : RequestValidator<GetStockOnHandRequest>
{
    public GetStockOnHandRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEqual(Guid.Empty).When(x => x.ProductId.HasValue)
            .WithMessage("Product id must not be empty.");

        RuleFor(x => x.WarehouseId)
            .NotEqual(Guid.Empty).When(x => x.WarehouseId.HasValue)
            .WithMessage("Warehouse id must not be empty.");
    }
}
