namespace AsistOff.MES.Integration.Tests.TestData;

/// <summary>
/// Shared constants for the endpoint integration tests. The tenant and admin
/// user are provisioned by the application's own dev seeder (configured in
/// <c>appsettings.Development.json</c>) at host startup, so tests authenticate
/// through the real <c>/api/auth/sign-in</c> endpoint.
/// </summary>
public static class IntegrationTestData
{
    public const string TenantName = "dev";
    public const string AdminEmail = "admin@dev.local";
    public const string AdminPassword = "Passw0rd!";
}
