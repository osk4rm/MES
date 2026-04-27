using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AsistOff.MES.Shared.Abstractions.Auth;

namespace AsistOff.MES.IntegrationTests.Infrastructure;

/// <summary>
/// Wires together the shared Postgres-backed test host with a per-test database
/// reset (Respawn) and a pre-authenticated <see cref="HttpClient"/> using the
/// dev tenant's admin JWT.
/// </summary>
[Collection(MesCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected MesApiFactory Factory { get; }

    /// <summary>HttpClient with a Bearer token for the dev tenant admin.</summary>
    protected HttpClient Client { get; private set; } = default!;

    /// <summary>HttpClient without authentication, for hitting AllowAnonymous endpoints.</summary>
    protected HttpClient AnonymousClient { get; private set; } = default!;

    protected IntegrationTestBase(MesApiFactory factory)
    {
        Factory = factory;
    }

    public async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();

        AnonymousClient = Factory.CreateClient();
        Client = Factory.CreateClient();

        var token = await SignInAsync(MesApiFactory.AdminEmail, MesApiFactory.AdminPassword);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Sends a sign-in request and returns the parsed token. Tests can call this
    /// directly to validate the auth endpoint or to obtain a token for an
    /// alternative user.
    /// </summary>
    protected async Task<JsonWebToken> SignInAsync(string email, string password)
    {
        using var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new { Email = email, Password = password });
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<JsonWebToken>(Json);
        return token!;
    }
}
