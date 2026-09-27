using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Production.Application.Features.Schedule;

public sealed class RescheduleGanttSegmentValidator : RequestValidator<RescheduleGanttSegmentRequest>
{
    internal const int MaxWindowDays = 31;
    internal const int MaxNotesLength = 2000;

    public RescheduleGanttSegmentValidator()
    {
        RuleFor(x => x.OperationNodeId)
            .NotEqual(Guid.Empty).WithMessage("Operation is required.");

        RuleFor(x => x.ProductionOrderId)
            .NotEqual(Guid.Empty).WithMessage("Production order is required.");

        RuleFor(x => x.MachineId)
            .NotEqual(Guid.Empty).WithMessage("Work Center is required.");

        RuleFor(x => x.PlannedStart)
            .NotEqual(default(DateTime)).WithMessage("Planned start is required.");

        RuleFor(x => x.PlannedEnd)
            .NotEqual(default(DateTime)).WithMessage("Planned end is required.");

        RuleFor(x => x.PlannedStart)
            .LessThan(x => x.PlannedEnd).WithMessage("Planned start must be before planned end.");

        RuleFor(x => x)
            .Must(x => x.PlannedStart == default || x.PlannedEnd == default
                || (x.PlannedEnd - x.PlannedStart).TotalDays <= MaxWindowDays)
            .WithMessage($"Moved window cannot exceed {MaxWindowDays} days.");

        RuleFor(x => x.ConcurrencyToken)
            .NotEmpty().WithMessage("Concurrency token is required. Reload the order and retry with the current token.");

        RuleFor(x => x.Notes)
            .MaximumLength(MaxNotesLength).WithMessage($"Notes cannot exceed {MaxNotesLength} characters.");
    }
}
