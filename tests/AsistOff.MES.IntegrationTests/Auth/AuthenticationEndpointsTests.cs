using AsistOff.MES.IntegrationTests.Infrastructure;

namespace AsistOff.MES.IntegrationTests.Auth;

public sealed class AuthenticationEndpointsTests : IntegrationTestBase
{
    public AuthenticationEndpointsTests(MesApiFactory factory) : base(factory) { }

    [Fact]
    public async Task SignIn_returns_jwt_for_seeded_admin()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/sign-in", new
        {
            Email = MesApiFactory.AdminEmail,
            Password = MesApiFactory.AdminPassword,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = await response.Content.ReadFromJsonAsync<TokenPayload>(Json);
        token.Should().NotBeNull();
        token!.AccessToken.Should().NotBeNullOrWhiteSpace();
        token.Role.Should().Be("tenant_admin");
    }

    [Fact]
    public async Task SignIn_returns_unauthorized_for_unknown_email()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/sign-in", new
        {
            Email = "nobody@nowhere.local",
            Password = "whatever-Passw0rd!",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignIn_returns_unauthorized_for_wrong_password()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/sign-in", new
        {
            Email = MesApiFactory.AdminEmail,
            Password = "definitely-wrong-Passw0rd!",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record TokenPayload(string AccessToken, string Role);
}
