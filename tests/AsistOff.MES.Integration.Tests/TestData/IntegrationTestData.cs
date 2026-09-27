namespace AsistOff.MES.Integration.Tests.TestData;

/// <summary>
/// Shared constants for the endpoint integration tests. The tenant and admin
/// user are provisioned by the application's own dev seeder at host startup,
/// with the seed password supplied explicitly in code by
/// <c>MesWebApplicationFactory</c> (issue #358: the shipped
/// <c>appsettings.Development.json</c> carries no default credential), so
/// tests authenticate through the real <c>/api/auth/sign-in</c> endpoint.
/// </summary>
public static class IntegrationTestData
{
    public const string TenantName = "dev";
    public const string AdminEmail = "admin@dev.local";
    public const string AdminPassword = "Passw0rd!";
}
