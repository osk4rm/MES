namespace AsistOff.MES.Configuration.Application.Features.Products.Common;

/// <summary>
/// GTIN/EAN validation and normalization for <c>Product.Ean</c>.
/// Accepts the standard GTIN lengths 8 (EAN-8), 12 (GTIN-12), 13 (EAN-13)
/// and 14 (GTIN-14); every value must be all digits with a valid GTIN check digit.
/// Empty or whitespace input normalizes to <c>null</c> (no EAN).
/// </summary>
public static class GtinValidator
{
    private static readonly int[] AllowedLengths = [8, 12, 13, 14];

    /// <summary>
    /// Trims the input; empty or whitespace becomes <c>null</c>.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    /// <summary>
    /// Returns <c>true</c> when the value is null/empty (no EAN) or a valid GTIN.
    /// </summary>
    public static bool IsValid(string? value)
    {
        var normalized = Normalize(value);

        if (normalized is null)
            return true;

        if (!AllowedLengths.Contains(normalized.Length))
            return false;

        for (var i = 0; i < normalized.Length; i++)
        {
            if (!char.IsAsciiDigit(normalized[i]))
                return false;
        }

        // GTIN check digit: positions counted from the right (check digit = position 1).
        // Odd positions weigh 1, even positions weigh 3; the weighted sum must be a multiple of 10.
        var sum = 0;

        for (var i = 0; i < normalized.Length; i++)
        {
            var digit = normalized[i] - '0';
            var positionFromRight = normalized.Length - i;
            sum += digit * (positionFromRight % 2 == 0 ? 3 : 1);
        }

        return sum % 10 == 0;
    }
}
