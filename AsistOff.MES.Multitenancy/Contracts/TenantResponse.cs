namespace AsistOff.MES.Multitenancy.Contracts;

public record TenantResponse(
    Guid Id,
    string Name,
    string? DisplayName,
    string? ContactEmail,
    TenantSettingsResponse Settings);