using System.Net.Http.Headers;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.TestData;
using Testcontainers.PostgreSql;

namespace AsistOff.MES.Integration.Tests.Infrastructure;

/// <summary>
/// xUnit collection fixture (shared by every integration test class) owning the
/// PostgreSQL Testcontainer and the application host. The container is created
/// once per test run; the host is started eagerly so migrations and seeding are
/// complete before the first test runs.
/// </summary>
public sealed class MesApplicationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("mes_integration")
        .WithUsername("mes")
        .WithPassword("mes")
        .Build();

    private MesWebApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new MesWebApplicationFactory(_postgres.GetConnectionString());

        // Force host startup: applies migrations and runs the dev seeder.
        using var _ = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public HttpClient CreateClient() => _factory.CreateClient();

    /// <summary>
    /// Connection string of the shared Testcontainers PostgreSQL, exposed so
    /// tests needing an isolated host (e.g. dedicated throttle budgets) can
    /// boot a second <see cref="MesWebApplicationFactory"/> against the same
    /// database without starting another container.
    /// </summary>
    public string PostgresConnectionString => _postgres.GetConnectionString();

    /// <summary>
    /// Application service provider, exposed so tests can assert database-level
    /// behaviour (e.g. <c>ON DELETE CASCADE</c>) that is not observable over HTTP.
    /// </summary>
    public IServiceProvider Services => _factory.Services;

    /// <summary>
    /// Returns a client carrying a real JWT obtained from <c>/api/auth/sign-in</c>
    /// for the seeded tenant administrator.
    /// </summary>
    public Task<HttpClient> CreateAuthenticatedClientAsync() =>
        CreateAuthenticatedClientAsync(IntegrationTestData.AdminEmail, IntegrationTestData.AdminPassword);

    /// <summary>
    /// Signs in as the given user and returns a client with the bearer token set.
    /// Sign-in issues the session over httpOnly cookies (no usable tokens in the
    /// body), so the access JWT is read back from the <c>mes_access</c>
    /// <c>Set-Cookie</c> header and presented as a header — proving the
    /// header transport keeps working during the cookie transition.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/sign-in", new { email, password });

        response.EnsureSuccessStatusCode();

        var accessToken = AuthCookieHelper.GetAccessToken(response);
        accessToken.Should().NotBeNullOrWhiteSpace();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }

    /// <summary>
    /// Provisions a brand-new tenant (and its tenant-admin user) through the real
    /// anonymous <c>POST /api/tenants</c> endpoint, for isolation tests.
    /// </summary>
    public async Task<(string Email, string Password)> CreateTenantAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"admin-{suffix}@integration.local";
        const string password = "Passw0rd!";

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = $"it-{suffix}",
            displayName = $"Integration Tenant {suffix}",
            contactEmail = email,
            settings = string.Empty,
            password,
            confirmPassword = password,
        });

        response.EnsureSuccessStatusCode();

        return (email, password);
    }
}
