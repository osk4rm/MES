using System.Net.Http.Headers;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.TestData;
using AsistOff.MES.Shared.Infrastructure.Auth;
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
    /// Cookie transport (#241): the sign-in body carries no tokens, so the
    /// access token is read from the <c>mes_access</c> Set-Cookie and replayed
    /// as a header — proving header callers keep working during transition.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/sign-in", new { email, password });

        response.EnsureSuccessStatusCode();

        var accessToken = ExtractCookie(response, AuthCookies.AccessCookieName);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }

    /// <summary>
    /// Extracts a cookie value from the <c>Set-Cookie</c> response headers,
    /// independent of cookie-container <c>Secure</c> handling.
    /// </summary>
    public static string ExtractCookie(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            foreach (var header in values)
            {
                var pair = header.Split(';', 2)[0];
                var separator = pair.IndexOf('=');
                if (separator > 0 && pair[..separator].Trim() == name)
                {
                    return pair[(separator + 1)..].Trim();
                }
            }
        }

        throw new InvalidOperationException($"Expected Set-Cookie '{name}' in the response.");
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
