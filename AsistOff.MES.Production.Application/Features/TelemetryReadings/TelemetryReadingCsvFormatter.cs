using System.Globalization;
using System.Text;

namespace AsistOff.MES.Production.Application.Features.TelemetryReadings;

/// <summary>
/// Formats telemetry readings as RFC 4180-style CSV (comma delimiter).
/// Values containing a comma, semicolon, quote or line break are wrapped
/// in double quotes with embedded quotes doubled.
/// </summary>
public static class TelemetryReadingCsvFormatter
{
    public const string Header = "Id,TagId,MachineId,ReadAt,DoubleValue,StringValue,Quality";

    /// <summary>Maximum data rows per export; the handler enforces this cap.</summary>
    public const int MaxRows = 5000;

    public static string Format(IEnumerable<TelemetryReadingResponse> items)
    {
        var builder = new StringBuilder();
        builder.AppendLine(Header);

        foreach (var item in items)
            builder.AppendLine(string.Join(',',
                Escape(item.Id.ToString()),
                Escape(item.TagId.ToString()),
                Escape(item.MachineId.ToString()),
                Escape(item.ReadAt.ToString("o", CultureInfo.InvariantCulture)),
                Escape(item.DoubleValue?.ToString(CultureInfo.InvariantCulture)),
                Escape(item.StringValue),
                Escape(((int)item.Quality).ToString(CultureInfo.InvariantCulture))));

        return builder.ToString();
    }

    internal static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Contains('"') || value.Contains(',') || value.Contains(';')
            || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }
}
