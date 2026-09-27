using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Auth;
using AsistOff.MES.Users.Application.Features.Authentication.Csrf;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Shared.Tests.Auth;

/// <summary>
/// Unit tests for the double-submit CSRF tokens (issue #376): issuance and
/// validation round-trip, tampered/expired/wrong-key rejection, the
/// cookie-vs-header match, the readable-but-hardened CSRF cookie, and the
/// anonymous issuance handler.
/// </summary>
public class CsrfTokensTests
{
    private const string SigningKey = "csrf-test-signing-key-with-32plus-bytes!!";

    [Fact]
    public void Issue_ThenValidate_RoundTrips()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        // Act
        var token = CsrfTokens.Issue(SigningKey, now: now);

        // Assert
        CsrfTokens.IsValid(token, SigningKey, now).Should().BeTrue();
    }

    [Fact]
    public void Validate_TamperedNonce_Rejects()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var token = CsrfTokens.Issue(SigningKey, now: now);
        var tampered = "X" + token[1..];

        // Act
        var valid = CsrfTokens.IsValid(tampered, SigningKey, now);

        // Assert
        valid.Should().BeFalse("the HMAC binds the nonce, so flipping it breaks the signature");
    }

    [Fact]
    public void Validate_TamperedSignature_Rejects()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var token = CsrfTokens.Issue(SigningKey, now: now);
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        // Act
        var valid = CsrfTokens.IsValid(tampered, SigningKey, now);

        // Assert
        valid.Should().BeFalse();
    }

    [Fact]
    public void Validate_TamperedExpiry_Rejects()
    {
        // Arrange — same nonce/signature, pushed expiry: the covered
        // nonce.expiry pair no longer matches the signature.
        var now = DateTimeOffset.UtcNow;
        var token = CsrfTokens.Issue(SigningKey, now: now);
        var parts = token.Split('.');
        var pushed = $"{parts[0]}.{long.Parse(parts[1]) + 3600}.{parts[2]}";

        // Act
        var valid = CsrfTokens.IsValid(pushed, SigningKey, now);

        // Assert
        valid.Should().BeFalse("extending the expiry must invalidate the signature");
    }

    [Fact]
    public void Validate_ExpiredToken_Rejects()
    {
        // Arrange
        var issuedAt = DateTimeOffset.UtcNow.AddHours(-25);
        var token = CsrfTokens.Issue(SigningKey, now: issuedAt);

        // Act
        var valid = CsrfTokens.IsValid(token, SigningKey, DateTimeOffset.UtcNow);

        // Assert
        valid.Should().BeFalse("a 24h token issued 25h ago is spent");
    }

    [Fact]
    public void Validate_WrongKey_Rejects()
    {
        // Arrange — a subdomain cookie-toss attacker cannot mint a matching
        // pair without the server signing key.
        var now = DateTimeOffset.UtcNow;
        var token = CsrfTokens.Issue(SigningKey, now: now);

        // Act
        var valid = CsrfTokens.IsValid(token, "another-signing-key-with-32plus-bytes!", now);

        // Assert
        valid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-token")]
    [InlineData("only.two")]
    [InlineData("a.b.c.d")]
    [InlineData("bm9uY2U.not-a-number.c2ln")]
    public void Validate_MalformedToken_Rejects(string? token)
    {
        // Act
        var valid = CsrfTokens.IsValid(token, SigningKey, DateTimeOffset.UtcNow);

        // Assert
        valid.Should().BeFalse();
    }

    [Fact]
    public void Validate_MissingSigningKey_Rejects()
    {
        // Arrange
        var token = CsrfTokens.Issue(SigningKey);

        // Act
        var valid = CsrfTokens.IsValid(token, null, DateTimeOffset.UtcNow);

        // Assert
        valid.Should().BeFalse("fail closed when the key is not configured");
    }

    [Fact]
    public void IsMatch_EqualCookieAndCandidate_Accepts()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var token = CsrfTokens.Issue(SigningKey, now: now);

        // Act
        var match = CsrfTokens.IsMatch(token, token, SigningKey, now);

        // Assert
        match.Should().BeTrue();
    }

    [Fact]
    public void IsMatch_MismatchedCandidate_Rejects()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var cookie = CsrfTokens.Issue(SigningKey, now: now);
        var other = CsrfTokens.Issue(SigningKey, now: now);

        // Act
        var match = CsrfTokens.IsMatch(cookie, other, SigningKey, now);

        // Assert
        match.Should().BeFalse("a live token from another issuance is not the cookie value");
    }

    [Theory]
    [InlineData(null, "candidate")]
    [InlineData("cookie", null)]
    [InlineData("cookie", "")]
    [InlineData(null, null)]
    public void IsMatch_MissingSide_Rejects(string? cookie, string? candidate)
    {
        // Act
        var match = CsrfTokens.IsMatch(cookie, candidate, SigningKey, DateTimeOffset.UtcNow);

        // Assert
        match.Should().BeFalse();
    }

    [Fact]
    public void BuildCookieOptions_IsReadableButHardened()
    {
        // Act
        var cookie = CsrfTokens.BuildCookieOptions();

        // Assert — readable so JavaScript can echo the token (it carries no
        // authority alone), but Secure + Lax + Path=/ like the session cookies.
        cookie.HttpOnly.Should().BeFalse("the frontend must read the token to echo it");
        cookie.Secure.Should().BeTrue();
        cookie.SameSite.Should().Be(SameSiteMode.Lax);
        cookie.Path.Should().Be("/");
    }

    [Fact]
    public void AppendCsrfCookie_SetsReadableCookie()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var token = CsrfTokens.Issue(SigningKey);

        // Act
        CsrfTokens.AppendCsrfCookie(context.Response, token);

        // Assert
        var setCookie = context.Response.Headers.SetCookie.ToString();
        setCookie.Should().StartWith($"{CsrfTokens.CookieName}={token}");
        setCookie.Should().Contain("secure");
        setCookie.Should().Contain("samesite=lax");
        setCookie.Should().NotContain("httponly");
    }

    [Fact]
    public async Task GetCsrfTokenHandler_ReturnsLiveToken()
    {
        // Arrange
        var options = new AuthOptions { IssuerSigningKey = SigningKey };
        var handler = new GetCsrfTokenRequestHandler(options, new FixedDateTimeProvider(DateTime.UtcNow));

        // Act
        var response = await handler.Handle(new GetCsrfTokenRequest(), default);

        // Assert
        response.CsrfToken.Should().NotBeNullOrWhiteSpace();
        CsrfTokens.IsValid(response.CsrfToken, SigningKey, DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public async Task GetCsrfTokenHandler_MissingSigningKey_Throws()
    {
        // Arrange
        var handler = new GetCsrfTokenRequestHandler(new AuthOptions(), new FixedDateTimeProvider(DateTime.UtcNow));

        // Act
        var act = () => handler.Handle(new GetCsrfTokenRequest(), default);

        // Assert — fail fast (500), never issue an unsigned token.
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow => utcNow;
    }
}
