using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;

namespace AsistOff.MES.Shared.Tests.Auth;

public class AuthCookiesTests
{
    private static AuthOptions Options() => new()
    {
        IssuerSigningKey = new string('k', 40),
        Issuer = "AsistOff.MES",
        Audience = "AsistOff.MES.Users",
        AccessTokenLifetime = TimeSpan.FromMinutes(15),
        RefreshTokenLifetime = TimeSpan.FromDays(7)
    };

    private static JsonWebToken Tokens() => new()
    {
        AccessToken = "access-token",
        RefreshToken = "refresh-token",
        Expires = 123,
        Id = Guid.NewGuid().ToString()
    };

    [Fact]
    public void BuildAccessOptions_IsHardened_AndMatchesAccessLifetime()
    {
        // Arrange
        var options = Options();

        // Act
        var cookie = AuthCookies.BuildAccessOptions(options);

        // Assert — HttpOnly + Secure + Lax + Path=/ with the access lifetime.
        cookie.HttpOnly.Should().BeTrue();
        cookie.Secure.Should().BeTrue();
        cookie.SameSite.Should().Be(SameSiteMode.Lax);
        cookie.Path.Should().Be("/");
        cookie.MaxAge.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void BuildAccessOptions_HonorsLegacyExpiryPrecedence()
    {
        // Arrange — legacy auth:Expiry wins over AccessTokenLifetime.
        var options = Options();
        options.Expiry = TimeSpan.FromHours(1);

        // Act
        var cookie = AuthCookies.BuildAccessOptions(options);

        // Assert
        cookie.MaxAge.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void BuildRefreshOptions_IsHardened_AndMatchesRefreshLifetime()
    {
        // Arrange
        var options = Options();

        // Act
        var cookie = AuthCookies.BuildRefreshOptions(options);

        // Assert
        cookie.HttpOnly.Should().BeTrue();
        cookie.Secure.Should().BeTrue();
        cookie.SameSite.Should().Be(SameSiteMode.Lax);
        cookie.Path.Should().Be("/");
        cookie.MaxAge.Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void AppendAuthCookies_SetsBothCookies_WithHardenedOptions()
    {
        // Arrange
        var cookies = new Mock<IResponseCookies>();
        string? accessValue = null;
        CookieOptions? accessOptions = null;
        string? refreshValue = null;
        CookieOptions? refreshOptions = null;
        cookies.Setup(c => c.Append(AuthCookies.AccessCookieName, It.IsAny<string>(), It.IsAny<CookieOptions>()))
            .Callback<string, string, CookieOptions>((_, v, o) => (accessValue, accessOptions) = (v, o));
        cookies.Setup(c => c.Append(AuthCookies.RefreshCookieName, It.IsAny<string>(), It.IsAny<CookieOptions>()))
            .Callback<string, string, CookieOptions>((_, v, o) => (refreshValue, refreshOptions) = (v, o));

        // Act
        AuthCookies.AppendAuthCookies(cookies.Object, Tokens(), Options());

        // Assert — both cookies carry the token values with hardened flags.
        accessValue.Should().Be("access-token");
        accessOptions.Should().NotBeNull();
        accessOptions!.HttpOnly.Should().BeTrue();
        accessOptions.Secure.Should().BeTrue();
        accessOptions.SameSite.Should().Be(SameSiteMode.Lax);
        accessOptions.Path.Should().Be("/");

        refreshValue.Should().Be("refresh-token");
        refreshOptions.Should().NotBeNull();
        refreshOptions!.HttpOnly.Should().BeTrue();
        refreshOptions.Secure.Should().BeTrue();
        refreshOptions.SameSite.Should().Be(SameSiteMode.Lax);
        refreshOptions.Path.Should().Be("/");
    }

    [Fact]
    public void ClearAuthCookies_ExpiresBothCookies_WithMatchingScope()
    {
        // Arrange
        var cookies = new Mock<IResponseCookies>();
        var appended = new List<(string Key, CookieOptions Options)>();
        cookies.Setup(c => c.Append(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CookieOptions>()))
            .Callback<string, string, CookieOptions>((k, _, o) => appended.Add((k, o)));

        // Act
        AuthCookies.ClearAuthCookies(cookies.Object);

        // Assert — both cookies expired with the same path/scope so browsers delete them.
        appended.Select(a => a.Key).Should()
            .BeEquivalentTo(AuthCookies.AccessCookieName, AuthCookies.RefreshCookieName);
        foreach (var (_, options) in appended)
        {
            options.Path.Should().Be("/");
            options.SameSite.Should().Be(SameSiteMode.Lax);
            options.Secure.Should().BeTrue();
            options.HttpOnly.Should().BeTrue();
            options.Expires.Should().NotBeNull();
            options.Expires!.Value.Should().BeOnOrBefore(DateTimeOffset.UtcNow);
        }
    }

    [Theory]
    [InlineData("Bearer header-token", "cookie-token", "header-token")]
    [InlineData("bearer header-token", "cookie-token", "header-token")]
    [InlineData(null, "cookie-token", "cookie-token")]
    [InlineData("", "cookie-token", "cookie-token")]
    [InlineData("Bearer   ", "cookie-token", "cookie-token")]
    [InlineData("Bearer header-token", null, "header-token")]
    [InlineData(null, null, null)]
    [InlineData("Basic abc", "cookie-token", "cookie-token")]
    public void GetAccessToken_PrefersHeader_ThenCookie(string? header, string? cookie, string? expected)
    {
        // Act
        var actual = AuthCookies.GetAccessToken(header, cookie);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void RequireSameOrigin_AbsentOrigin_PassesForNonBrowserCallers()
    {
        // Act
        var act = () => AuthCookies.RequireSameOrigin(null, "mes.local");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RequireSameOrigin_SameHost_Passes()
    {
        // Act — same host on a different port (dev proxy) still passes.
        var act = () => AuthCookies.RequireSameOrigin("http://mes.local:5173", "mes.local:5080".Split(':')[0]);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RequireSameOrigin_ForeignHost_ThrowsValidationException()
    {
        // Act
        var act = () => AuthCookies.RequireSameOrigin("https://evil.example", "mes.local");

        // Assert — CSRF backstop rejects with 400 semantics.
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void RequireSameOrigin_MalformedOrigin_ThrowsValidationException()
    {
        // Act
        var act = () => AuthCookies.RequireSameOrigin("not-a-uri", "mes.local");

        // Assert
        act.Should().Throw<ValidationException>();
    }
}
