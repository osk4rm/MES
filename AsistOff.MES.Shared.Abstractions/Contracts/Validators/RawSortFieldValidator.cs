using AsistOff.MES.Shared.Abstractions.Contracts.Sorting;
using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Shared.Abstractions.Contracts.Validators;

public class RawSortFieldValidator : RequestValidator<string>
{
    private const int FieldNameIndex = 0;
    private const int SortingOrderIndex = 1;

    public RawSortFieldValidator(IReadOnlyCollection<string>? supportedSortFields = null)
    {
        RuleFor(x => x)
            .Custom((rawSort, context) =>
            {
                if (supportedSortFields is null)
                    return;
                
                var sortPhraseParts = rawSort.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (sortPhraseParts.Length < 1)
                {
                    context.AddFailure("RawSort", "Sort parameter must include a field name");
                    return;
                }

                if (sortPhraseParts.Any(string.IsNullOrWhiteSpace))
                {
                    context.AddFailure("RawSort", "Sort parameter cannot be empty");
                }

                // Validate field name is supported (case-insensitive)
                if (!supportedSortFields.Any(s => s.Equals(sortPhraseParts[FieldNameIndex], StringComparison.OrdinalIgnoreCase)))
                {
                    context.AddFailure("RawSort", $"Sort field '{sortPhraseParts[FieldNameIndex]}' must be supported.");
                }

                // If order provided, validate it (supports asc/desc shorthands)
                if (sortPhraseParts.Length > 1)
                {
                    var token = sortPhraseParts[SortingOrderIndex];
                    if (!(token.Equals("asc", StringComparison.OrdinalIgnoreCase)
                          || token.Equals("desc", StringComparison.OrdinalIgnoreCase)
                          || Enum.TryParse(token, true, out SortOrder _)))
                    {
                        context.AddFailure("RawSort", "Invalid sort order");
                    }
                }
            });
    }
}