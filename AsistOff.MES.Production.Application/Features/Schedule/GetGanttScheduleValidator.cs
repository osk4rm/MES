using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Production.Application.Features.Schedule;

public sealed class GetGanttScheduleValidator : RequestValidator<GetGanttScheduleRequest>
{
    internal const int MaxWindowDays = 31;

    public GetGanttScheduleValidator()
    {
        RuleFor(x => x.From)
            .NotEqual(default(DateOnly)).WithMessage("Date window is required.");

        RuleFor(x => x.To)
            .NotEqual(default(DateOnly)).WithMessage("Date window is required.");

        RuleFor(x => x.From)
            .LessThanOrEqualTo(x => x.To).WithMessage("From must not be after To.");

        RuleFor(x => x)
            .Must(x => x.From == default || x.To == default || x.To.DayNumber - x.From.DayNumber + 1 <= MaxWindowDays)
            .WithMessage($"Time window cannot exceed {MaxWindowDays} days.");
    }
}
