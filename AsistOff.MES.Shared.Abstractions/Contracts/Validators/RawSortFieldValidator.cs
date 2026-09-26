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
                if (string.IsNullOrWhiteSpace(rawSort))
                {
                    context.AddFailure("RawSort", "Sort parameter must include a field name");
                    return;
                }

                var sortPhraseParts = rawSort.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (sortPhraseParts.Length < 1 || sortPhraseParts.Length > 2)
                {
                    context.AddFailure("RawSort", "Sort parameter must be 'Field' or 'Field,asc|desc'");
                    return;
                }

                if (sortPhraseParts.Any(string.IsNullOrWhiteSpace))
                {
                    context.AddFailure("RawSort", "Sort parameter cannot be empty");
                }

                var fieldName = sortPhraseParts[FieldNameIndex];

                // Strict identifier grammar first: nothing outside
                // [A-Za-z_][A-Za-z0-9_]* may pass, so Dynamic LINQ method
                // calls, navigation traversal (dots) and expressions are
                // rejected before the whitelist is even consulted (#311).
                if (!SortableResolver.IsValidFieldName(fieldName))
                {
                    context.AddFailure("RawSort", $"Sort field '{fieldName}' must be supported.");
                    return;
                }

                // Validate field name is supported (case-insensitive).
                // A null whitelist means grammar-only validation; an empty
                // whitelist means sorting is not allowed at all (fail closed).
                if (supportedSortFields is null)
                {
                    // Grammar already checked above; nothing more to enforce.
                }
                else if (!supportedSortFields.Any(s => s.Equals(fieldName, StringComparison.OrdinalIgnoreCase)))
                {
                    context.AddFailure("RawSort", $"Sort field '{fieldName}' must be supported.");
                }

                // If order provided, only asc/desc are accepted.
                if (sortPhraseParts.Length > 1)
                {
                    var token = sortPhraseParts[SortingOrderIndex];
                    if (!SortableResolver.IsValidOrderToken(token))
                    {
                        context.AddFailure("RawSort", "Invalid sort order");
                    }
                }
            });
    }
}
