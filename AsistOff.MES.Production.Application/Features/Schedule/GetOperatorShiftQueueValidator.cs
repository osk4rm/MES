using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Production.Application.Features.Schedule;

public sealed class GetOperatorShiftQueueValidator : RequestValidator<GetOperatorShiftQueueRequest>
{
    internal const int MaxTake = 200;

    public GetOperatorShiftQueueValidator()
    {
        RuleFor(x => x.OperatorCode)
            .NotEmpty().WithMessage("Operator code is required.")
            .MaximumLength(100).WithMessage("Operator code is too long.");

        When(x => x.Take.HasValue, () =>
        {
            RuleFor(x => x.Take!.Value)
                .GreaterThanOrEqualTo(1).WithMessage("Take must be at least 1.");
        });
    }
}
