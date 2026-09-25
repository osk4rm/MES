using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Configuration.Application.Features.StockMovements.Browse;

public class BrowseStockMovementsRequestValidator : RequestValidator<BrowseStockMovementsRequest>
{
    public BrowseStockMovementsRequestValidator()
    {
        RuleFor(x => x.ConfirmationId)
            .NotNull().WithMessage("Confirmation id is required.")
            .NotEqual(Guid.Empty).WithMessage("Confirmation id is required.");
    }
}
