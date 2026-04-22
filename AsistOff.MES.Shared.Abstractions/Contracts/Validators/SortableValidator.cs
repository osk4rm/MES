using AsistOff.MES.Shared.Abstractions.Contracts.Sorting;
using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Shared.Abstractions.Contracts.Validators;

public class SortableValidator : RequestValidator<ISortable>
{
    public SortableValidator()
    {
        When(x => x.RawSort != null && x.RawSort.Count != 0, () =>
        {
            RuleFor(x => x.RawSort)
                .Must(x => x.All(y => x.Count(z => z == y) == 1))
                .WithMessage("Sort fields must be unique.");

            RuleForEach(x => x.RawSort)
                .SetValidator(x => new RawSortFieldValidator(x.SupportedSortFields));
        });
    }
}