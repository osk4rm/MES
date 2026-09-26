using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Production.Application.Features.ShiftHandovers;

public sealed class GetShiftHandoverContextValidator : RequestValidator<GetShiftHandoverContextRequest>
{
    internal const double MaxWindowHours = 24;
    internal const int DefaultConfirmationPageSize = 20;
    internal const int MaxConfirmationPageSize = 100;

    public GetShiftHandoverContextValidator()
    {
        RuleFor(x => x.From)
            .NotEmpty().WithMessage("Time window is required.");

        RuleFor(x => x.To)
            .NotEmpty().WithMessage("Time window is required.");

        RuleFor(x => x.From)
            .LessThan(x => x.To).WithMessage("From must be before To.");

        RuleFor(x => x)
            .Must(x => (x.To.ToUniversalTime() - x.From.ToUniversalTime()).TotalHours <= MaxWindowHours)
            .WithMessage($"Time window cannot exceed {MaxWindowHours} hours.");

        RuleFor(x => x.MachineId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("Machine is invalid.");

        When(x => x.ConfirmationPage.HasValue, () =>
        {
            RuleFor(x => x.ConfirmationPage!.Value)
                .GreaterThanOrEqualTo(1).WithMessage("Confirmation page must be at least 1.");
        });

        When(x => x.ConfirmationPageSize.HasValue, () =>
        {
            RuleFor(x => x.ConfirmationPageSize!.Value)
                .InclusiveBetween(1, MaxConfirmationPageSize)
                .WithMessage($"Confirmation page size must be between 1 and {MaxConfirmationPageSize}.");
        });
    }
}
