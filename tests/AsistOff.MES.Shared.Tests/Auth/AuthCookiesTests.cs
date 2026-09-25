using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Infrastructure.Auth;
using AsistOff.MES.Users.Application.Features.Authentication.Refresh;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace AsistOff.MES.Shared.Tests.Auth;

/// <summary>
/// Unit tests for the httpOnly cookie transport (issue #241, slice 1 of 2):
/// cookie flags, lifetimes, origin guard and the anonymous-refresh contract
/// (missing token is 401 from the handler, never 400 from validation).
/// Rotation replay/revocation and tenant binding of the stored rows are
/// covered by <c>Users/RefreshTokenFlowTests</c>.
/// </summary>
public class AuthCookiesTests
{
    private static AuthOptions Options(TimeSpan? access = null, TimeSpan? refresh = null) => new()
    {
        IssuerSigningKey = new string('k', 40),
        Issuer = "AsistOff.MES",
        Audience = "AsistOff.MES.Users",
        AccessTokenLifetime = access ?? TimeSpan.FromMinutes(15),
        RefreshTokenLifetime = refresh ?? TimeSpan.FromDays(7)
    };

    [Fact]
    public void BuildAccessOptions_HasRequiredFlags_AndMatchesAccessLifetime()
    {
        // Arrange
        var options = Options(access: TimeSpan.FromMinutes(15));
        var now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

        // Act
        var cookie = AuthCookies.BuildAccessOptions(options, now);

        // Assert
        cookie.HttpOnly.Should().BeTrue();
        cookie.Secure.Should().BeTrue();
        cookie.SameSite.Should().Be(SameSiteMode.Lax);
        cookie.Path.Should().Be("/");
        cookie.MaxAge.Should().Be(TimeSpan.FromMinutes(15));
        cookie.Expires.Should().Be(now.Add(TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void BuildAccessOptions_HonorsLegacyExpiryPrecedence()
    {
        // Arrange — legacy auth:Expiry wins over AccessTokenLifetime.
        var options = Options(access: TimeSpan.FromMinutes(15));
        options.Expiry = TimeSpan.FromHours(1);
        var now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

        // Act
        var cookie = AuthCookies.BuildAccessOptions(options, now);

        // Assert
        cookie.MaxAge.Should().Be(TimeSpan.FromHours(1));
        cookie.Expires.Should().Be(now.Add(TimeSpan.FromHours(1)));
    }

    [Fact]
    public void BuildRefreshOptions_HasRequiredFlags_AndMatchesRefreshLifetime()
    {
        // Arrange
        var options = Options(refresh: TimeSpan.FromDays(7));
        var now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

        // Act
        var cookie = AuthCookies.BuildRefreshOptions(options, now);

        // Assert
        cookie.HttpOnly.Should().BeTrue();
        cookie.Secure.Should().BeTrue();
        cookie.SameSite.Should().Be(SameSiteMode.Lax);
        cookie.Path.Should().Be("/");
        cookie.MaxAge.Should().Be(TimeSpan.FromDays(7));
        cookie.Expires.Should().Be(now.Add(TimeSpan.FromDays(7)));
    }

    [Fact]
    public void BuildClearOptions_IsExpired()
    {
        // Arrange & Act
        var cookie = AuthCookies.BuildClearOptions();

        // Assert — expired Set-Cookie that drops the session.
        cookie.HttpOnly.Should().BeTrue();
        cookie.Secure.Should().BeTrue();
        cookie.SameSite.Should().Be(SameSiteMode.Lax);
        cookie.Path.Should().Be("/");
        cookie.Expires.Should().Be(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void AppendAuthCookies_SetsBothCookies_WithRequiredFlags()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var options = Options();

        // Act
        AuthCookies.AppendAuthCookies(context.Response, "access-123", "refresh-456", options, DateTimeOffset.UtcNow);

        // Assert — flag names are lowercased by the framework.
        var setCookie = context.Response.Headers.SetCookie.ToString();
        setCookie.Should().Contain($"{AuthCookies.AccessCookieName}=access-123");
        setCookie.Should().Contain($"{AuthCookies.RefreshCookieName}=refresh-456");
        setCookie.ToLowerInvariant().Should().Contain("httponly");
        setCookie.ToLowerInvariant().Should().Contain("secure");
        setCookie.Should().ContainEquivalentOf("SameSite=Lax");
        setCookie.Should().ContainEquivalentOf("Path=/");
    }

    [Fact]
    public void ClearAuthCookies_SetsExpiredCookies()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        AuthCookies.ClearAuthCookies(context.Response);

        // Assert
        var setCookie = context.Response.Headers.SetCookie.ToString();
        setCookie.Should().Contain($"{AuthCookies.AccessCookieName}=");
        setCookie.Should().Contain($"{AuthCookies.RefreshCookieName}=");
        setCookie.ToLowerInvariant().Should().Contain("httponly");
        setCookie.ToLowerInvariant().Should().Contain("expires=");
    }

    [Fact]
    public void TryGetRefreshToken_ReturnsCookieValue()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{AuthCookies.RefreshCookieName}=opaque-token";

        // Act
        var found = AuthCookies.TryGetRefreshToken(context.Request, out var token);

        // Assert
        found.Should().BeTrue();
        token.Should().Be("opaque-token");
    }

    [Fact]
    public void TryGetRefreshToken_MissingCookie_ReturnsFalse()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var found = AuthCookies.TryGetRefreshToken(context.Request, out var token);

        // Assert
        found.Should().BeFalse();
        token.Should().BeNull();
    }

    [Fact]
    public void TryGetAccessToken_BlankCookie_ReturnsFalse()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{AuthCookies.AccessCookieName}=%20";

        // Act
        var found = AuthCookies.TryGetAccessToken(context.Request, out var token);

        // Assert
        found.Should().BeFalse();
        token.Should().BeNull();
    }

    [Fact]
    public void ValidateOrigin_WithoutOriginHeader_Passes()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("api.example.com");

        // Act
        var act = () => AuthCookies.ValidateOrigin(context.Request, configuration: null);

        // Assert — same-origin form posts and non-browser clients pass.
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateOrigin_SameHost_Passes()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("api.example.com");
        context.Request.Headers.Origin = "https://api.example.com";

        // Act
        var act = () => AuthCookies.ValidateOrigin(context.Request, configuration: null);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateOrigin_AllowedOrigin_Passes()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("api.example.com");
        context.Request.Headers.Origin = "https://app.example.com";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["cors:allowedOrigins:0"] = "https://app.example.com"
            })
            .Build();

        // Act
        var act = () => AuthCookies.ValidateOrigin(context.Request, configuration);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateOrigin_CrossOrigin_ThrowsForbidden()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("api.example.com");
        context.Request.Headers.Origin = "https://evil.example.com";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["cors:allowedOrigins:0"] = "https://app.example.com"
            })
            .Build();

        // Act
        var act = () => AuthCookies.ValidateOrigin(context.Request, configuration);

        // Assert — malicious site cannot mint or rotate a session.
        act.Should().Throw<ForbiddenException>();
    }

    [Fact]
    public void RefreshValidator_AllowsEmptyToken_SoMissingCookieIs401Not400()
    {
        // Arrange — cookie fallback posts an empty body; the handler (not the
        // validator) owns the 401 for a missing token.
        var validator = new RefreshTokenRequestValidator();

        // Act
        var empty = validator.Validate(new RefreshTokenRequest(string.Empty));
        var missing = validator.Validate(new RefreshTokenRequest(null));

        // Assert
        empty.IsValid.Should().BeTrue();
        missing.IsValid.Should().BeTrue();
    }
}
