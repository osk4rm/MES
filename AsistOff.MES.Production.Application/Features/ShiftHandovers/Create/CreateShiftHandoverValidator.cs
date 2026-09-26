using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Production.Application.Features.ShiftHandovers.Create;

public sealed class CreateShiftHandoverValidator : RequestValidator<CreateShiftHandoverRequest>
{
    internal const double MaxWindowHours = 24;
    internal const int MaxNotesLength = 2000;

    public CreateShiftHandoverValidator()
    {
        RuleFor(x => x.MachineId)
            .NotEmpty().WithMessage("Machine is required.");

        RuleFor(x => x.From)
            .NotEmpty().WithMessage("Time window is required.");

        RuleFor(x => x.To)
            .NotEmpty().WithMessage("Time window is required.");

        RuleFor(x => x.From)
            .LessThan(x => x.To).WithMessage("From must be before To.");

        RuleFor(x => x)
            .Must(x => (x.To.ToUniversalTime() - x.From.ToUniversalTime()).TotalHours <= MaxWindowHours)
            .WithMessage($"Time window cannot exceed {MaxWindowHours} hours.");

        RuleFor(x => x.Notes)
            .NotEmpty().WithMessage("Notes are required.")
            .MaximumLength(MaxNotesLength).WithMessage($"Notes cannot exceed {MaxNotesLength} characters.");
    }
}
