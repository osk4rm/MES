using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Configuration.Application.Features.Products.Browse;

public class BrowseProductsRequestValidator : RequestValidator<BrowseProductsRequest>
{
    public BrowseProductsRequestValidator()
    {
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

        When(x => !string.IsNullOrWhiteSpace(x.Search), () =>
        {
            RuleFor(x => x.Search!)
                .MaximumLength(100).WithMessage("Search must not exceed 100 characters.");
        });
    }
}
