using AsistOff.MES.Shared.Abstractions.Audit;
using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Production.Application.Features.AuditEvents.Browse;

public class BrowseAuditEventsRequestValidator : RequestValidator<BrowseAuditEventsRequest>
{
    public BrowseAuditEventsRequestValidator()
    {
        When(x => !string.IsNullOrWhiteSpace(x.EntityName), () =>
        {
            RuleFor(x => x.EntityName)
                .Must(name => AuditEntityNames.IsPilot(name))
                .WithMessage($"Entity name must be one of: {string.Join(", ", AuditEntityNames.Pilots)}.");
        });

        When(x => x.EntityId.HasValue, () =>
        {
            RuleFor(x => x.EntityName)
                .NotEmpty().WithMessage("Entity name is required when entity id is provided.")
                .Must(name => AuditEntityNames.IsPilot(name))
                .WithMessage($"Entity name must be one of: {string.Join(", ", AuditEntityNames.Pilots)}.");
        });

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
