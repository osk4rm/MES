using AsistOff.MES.Shared.Abstractions.Contracts.Sorting;
using FluentValidation;

namespace AsistOff.MES.Shared.Abstractions.Contracts.Validators;

public class RawSortFieldValidator : AbstractValidator<string>
{
    private const int FieldNameIndex = 0;
    private const int SortingOrderIndex = 1;

    public RawSortFieldValidator(IReadOnlyCollection<string> supportedSortFields)
    {
        RuleFor(x => x)
            .Custom((rawSort, context) =>
            {
                var sortPhraseParts = rawSort.Split(",");

                if (sortPhraseParts.Length != 2)
                {
                    context.AddFailure("RawSort", "Sort parameter must have both name and sort order");
                }

                if (sortPhraseParts.Any(string.IsNullOrWhiteSpace))
                {
                    context.AddFailure("RawSort", "Sort parameter cannot be empty");
                }

                foreach (var field in supportedSortFields)
                {
                    if (!supportedSortFields.Any(s =>
                            s.Equals(sortPhraseParts[FieldNameIndex], StringComparison.OrdinalIgnoreCase)))
                    {
                        context.AddFailure("RawSort",
                            $"Sort field '{sortPhraseParts[FieldNameIndex]}' must be supported.");
                    }

                    if (sortPhraseParts.Length > 1 && !Enum.TryParse(sortPhraseParts[SortingOrderIndex].Trim(), true,
                            out SortOrder parsedOrder))
                    {
                        context.AddFailure("RawSort", "Invalid sort order");
                    }
                }
            });
    }
}