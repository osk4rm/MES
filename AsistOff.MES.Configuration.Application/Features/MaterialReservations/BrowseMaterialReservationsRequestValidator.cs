using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Configuration.Application.Features.MaterialReservations;

public class BrowseMaterialReservationsRequestValidator : RequestValidator<BrowseMaterialReservationsRequest>
{
    public BrowseMaterialReservationsRequestValidator()
    {
        RuleFor(x => x.ProductionOrderId)
            .NotEqual(Guid.Empty).When(x => x.ProductionOrderId.HasValue)
            .WithMessage("Production order id must not be empty.");

        RuleFor(x => x.ProductId)
            .NotEqual(Guid.Empty).When(x => x.ProductId.HasValue)
            .WithMessage("Product id must not be empty.");

        RuleFor(x => x.WarehouseId)
            .NotEqual(Guid.Empty).When(x => x.WarehouseId.HasValue)
            .WithMessage("Warehouse id must not be empty.");

        When(x => x.PageNumber.HasValue || x.PageSize.HasValue, () =>
        {
            RuleFor(x => x.PageNumber)
                .NotNull().WithMessage("Page number is required when paging.")
                .GreaterThan(0).WithMessage("Page number must be greater than 0.");

            RuleFor(x => x.PageSize)
                .NotNull().WithMessage("Page size is required when paging.")
                .GreaterThanOrEqualTo(1).WithMessage("Page size must be at least 1.")
                .LessThanOrEqualTo(100).WithMessage("Page size must not exceed 100.");
        });
    }
}
