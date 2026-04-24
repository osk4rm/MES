namespace AsistOff.MES.Multitenancy.Contracts;

public record TenantSettingsResponse(string Country)
{
    public TenantSettingsResponse() : this(string.Empty) { }
}