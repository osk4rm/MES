namespace AsistOff.MES.Multitenancy.Seeding;

public sealed class DevTenantSeedOptions
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; }
    public List<DevTenantSeed> Tenants { get; set; } = new();
}

public sealed class DevTenantSeed
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
}
