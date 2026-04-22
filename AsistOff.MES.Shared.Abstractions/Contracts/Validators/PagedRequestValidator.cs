using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Shared.Abstractions.Contracts.Validators;

public class PagedRequestValidator : RequestValidator<IPagedRequest>
{
    public PagedRequestValidator()
    {
        When(x => x.MaxPageSize.HasValue, () =>
        {
            RuleFor(x => new { x.PageNumber, x.PageSize })
                .Must(x => x.PageNumber.HasValue && x.PageSize.HasValue)
                .WithMessage("Page number and page size must be provided");

            RuleFor(x => x.PageNumber)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Page number must be greater or equal to 0");

            RuleFor(x => x.PageSize)
                .GreaterThanOrEqualTo(1)
                .LessThanOrEqualTo(x => x.MaxPageSize);
        });
    }
}