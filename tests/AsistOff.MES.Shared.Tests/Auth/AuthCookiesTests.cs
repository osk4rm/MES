using AsistOff.MES.Shared.Infrastructure.Auth;
using AsistOff.MES.Users.Application.Features.Authentication.Refresh;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Shared.Tests.Auth;

public class AuthCookiesTests
{
    private static AuthOptions Options() => new()
    {
        IssuerSigningKey = new string('k', 40),
        Issuer = "AsistOff.MES",
        Audience = "AsistOff.MES",
        AccessTokenLifetime = TimeSpan.FromMinutes(15),
        RefreshTokenLifetime = TimeSpan.FromDays(7)
    };

    [Fact]
    public void BuildAccessCookieOptions_UsesHardenedFlagsAndAccessLifetime()
    {
        // Arrange
        var options = Options();

        // Act
        var cookie = AuthCookies.BuildAccessCookieOptions(options);

        // Assert
        cookie.HttpOnly.Should().BeTrue("JavaScript must never read the token");
        cookie.Secure.Should().BeTrue("the token must only travel over TLS");
        cookie.SameSite.Should().Be(SameSiteMode.Lax);
        cookie.Path.Should().Be("/");
        cookie.MaxAge.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void BuildRefreshCookieOptions_UsesRefreshLifetime()
    {
        // Arrange
        var options = Options();

        // Act
        var cookie = AuthCookies.BuildRefreshCookieOptions(options);

        // Assert
        cookie.HttpOnly.Should().BeTrue();
        cookie.Secure.Should().BeTrue();
        cookie.SameSite.Should().Be(SameSiteMode.Lax);
        cookie.Path.Should().Be("/");
        cookie.MaxAge.Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void GetAccessLifetime_LegacyExpiryOverride_Wins()
    {
        // Arrange
        var options = Options();
        options.Expiry = TimeSpan.FromHours(1);

        // Act
        var lifetime = AuthCookies.GetAccessLifetime(options);

        // Assert
        lifetime.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void AppendAuthCookies_SetsBothCookiesWithFlags()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        AuthCookies.AppendAuthCookies(context.Response, "access-jwt", "opaque-refresh", Options());

        // Assert
        var setCookies = context.Response.Headers.SetCookie.ToList();
        setCookies.Should().HaveCount(2);
        var access = setCookies.Single(c => c!.StartsWith($"{AuthCookies.AccessCookieName}=access-jwt"));
        var refresh = setCookies.Single(c => c!.StartsWith($"{AuthCookies.RefreshCookieName}=opaque-refresh"));

        foreach (var cookie in new[] { access, refresh })
        {
            cookie.Should().Contain("httponly");
            cookie.Should().Contain("secure");
            cookie.Should().Contain("samesite=lax");
            cookie.Should().Contain("path=/");
        }
    }

    [Fact]
    public void ClearAuthCookies_ExpiresBothCookies()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        AuthCookies.ClearAuthCookies(context.Response);

        // Assert
        var setCookies = context.Response.Headers.SetCookie.ToList();
        setCookies.Should().HaveCount(2);
        setCookies.Should().OnlyContain(c =>
            c!.StartsWith($"{AuthCookies.AccessCookieName}=") || c.StartsWith($"{AuthCookies.RefreshCookieName}="));
        setCookies.Should().OnlyContain(c => c!.Contains("1970") || c.Contains("max-age=0"));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    public void IsOriginAllowed_WithoutOrigin_Allows(string? origin, bool expected)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("localhost", 5080);
        if (origin is not null)
        {
            context.Request.Headers.Origin = origin;
        }

        // Act
        var allowed = AuthCookies.IsOriginAllowed(context.Request);

        // Assert
        allowed.Should().Be(expected);
    }

    [Fact]
    public void IsOriginAllowed_SameHostDifferentPort_Allows()
    {
        // Arrange — the local Vite dev server calls the API from another port.
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("localhost", 5080);
        context.Request.Headers.Origin = "http://localhost:5173";

        // Act
        var allowed = AuthCookies.IsOriginAllowed(context.Request);

        // Assert
        allowed.Should().BeTrue();
    }

    [Fact]
    public void IsOriginAllowed_CrossHost_Rejects()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("api.example.com");
        context.Request.Headers.Origin = "https://evil.example";

        // Act
        var allowed = AuthCookies.IsOriginAllowed(context.Request);

        // Assert
        allowed.Should().BeFalse();
    }

    [Fact]
    public void IsOriginAllowed_MalformedOrigin_Rejects()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("api.example.com");
        context.Request.Headers.Origin = "not-a-uri";

        // Act
        var allowed = AuthCookies.IsOriginAllowed(context.Request);

        // Assert
        allowed.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshValidator_NullToken_AllowsCookieSuppliedFlow()
    {
        // Arrange
        var validator = new RefreshTokenRequestValidator();

        // Act
        var result = await validator.ValidateAsync(new RefreshTokenRequest(null));

        // Assert — missing body token is not a 400; the handler rejects it with 401.
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshValidator_BlankToken_Rejects()
    {
        // Arrange
        var validator = new RefreshTokenRequestValidator();

        // Act
        var result = await validator.ValidateAsync(new RefreshTokenRequest(string.Empty));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshValidator_SuppliedToken_Allows()
    {
        // Arrange
        var validator = new RefreshTokenRequestValidator();

        // Act
        var result = await validator.ValidateAsync(new RefreshTokenRequest("opaque-token"));

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
