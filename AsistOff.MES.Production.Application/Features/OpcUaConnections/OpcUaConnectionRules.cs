using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Exceptions;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections;

/// <summary>Shared validation for OPC UA connection create/update/test so all paths enforce the same rules.</summary>
internal static class OpcUaConnectionRules
{
    public const int MaxEndpointUrlLength = 512;
    public const int MaxLastErrorLength = 512;
    public const int MinPollIntervalSeconds = 5;
    public const int MaxPollIntervalSeconds = 3600;

    private static readonly string[] AllowedSchemes = ["http", "https", "opc.tcp", "opc.http"];

    public static void Validate(
        Guid machineId,
        string endpointUrl,
        OpcUaSecurityPolicy securityPolicy,
        int pollIntervalSeconds)
    {
        if (machineId == Guid.Empty)
            throw new ValidationException(nameof(machineId), "Machine is required.");
        ValidateEndpointUrl(endpointUrl);
        if (!Enum.IsDefined(securityPolicy))
            throw new ValidationException(nameof(securityPolicy), "Security policy is not supported.");
        if (pollIntervalSeconds is < MinPollIntervalSeconds or > MaxPollIntervalSeconds)
            throw new ValidationException(nameof(pollIntervalSeconds), $"Poll interval must be between {MinPollIntervalSeconds} and {MaxPollIntervalSeconds} seconds.");
    }

    /// <summary>Validates the endpoint URL shape: absolute URI with an allowed scheme.</summary>
    public static string ValidateEndpointUrl(string endpointUrl)
    {
        if (string.IsNullOrWhiteSpace(endpointUrl))
            throw new ValidationException(nameof(endpointUrl), "Endpoint URL is required.");

        var trimmed = endpointUrl.Trim();
        if (trimmed.Length > MaxEndpointUrlLength)
            throw new ValidationException(nameof(endpointUrl), $"Endpoint URL must be at most {MaxEndpointUrlLength} characters.");

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || !AllowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
            throw new ValidationException(nameof(endpointUrl), "Endpoint URL must be an absolute http, https, opc.tcp or opc.http URL.");

        return trimmed;
    }

    public static void ValidateLastError(string? lastError)
    {
        if (lastError is not null && lastError.Length > MaxLastErrorLength)
            throw new ValidationException(nameof(lastError), $"Last error must be at most {MaxLastErrorLength} characters.");
    }
}
