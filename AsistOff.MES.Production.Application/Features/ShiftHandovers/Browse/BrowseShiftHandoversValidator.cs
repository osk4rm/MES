using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Production.Application.Features.ShiftHandovers.Browse;

public sealed class BrowseShiftHandoversValidator : RequestValidator<BrowseShiftHandoversRequest>
{
    public BrowseShiftHandoversValidator()
    {
        RuleFor(x => x.MachineId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("Machine is invalid.");

        When(x => x.From.HasValue && x.To.HasValue, () =>
        {
            RuleFor(x => x.From!.Value)
                .LessThan(x => x.To!.Value).WithMessage("From must be before To.");
        });

        When(x => x.PageNumber.HasValue, () =>
        {
            RuleFor(x => x.PageNumber!.Value)
                .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");
        });

        When(x => x.PageSize.HasValue, () =>
        {
            RuleFor(x => x.PageSize!.Value)
                .InclusiveBetween(1, 100)
                .WithMessage("Page size must be between 1 and 100.");
        });
    }
}
