using System.Text.Json;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Users.Api.Filters;

/// <summary>
/// Double-submit CSRF gate for the cookie-writing auth endpoints
/// (issue #376). Runs as an authorization filter so the JSON body field
/// fallback can be read before model binding consumes the stream:
/// <list type="number">
/// <item>Same-host Origin baseline first (preserves the
/// <c>AuthenticationController</c> guard: requests without an
/// <c>Origin</c> header from non-browser clients pass through).</item>
/// <item>The echoed token — <c>X-CSRF-Token</c> header, else the
/// <c>csrfToken</c>/<c>csrf_token</c> JSON body field — must equal the
/// <c>mes_csrf</c> cookie value and carry a live, correctly signed token,
/// otherwise <see cref="ForbiddenException"/> (403) and the handler never
/// runs, so a rejected refresh rotates nothing and a rejected sign-out
/// clears nothing.</item>
/// </list>
/// Ordered after <c>[Authorize]</c> so an unauthenticated sign-out still
/// fails with 401 instead of 403.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireCsrfTokenAttribute : Attribute, IAsyncAuthorizationFilter, IOrderedFilter
{
    /// <inheritdoc />
    public int Order => 100;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;

        if (!AuthCookies.IsOriginAllowed(request))
        {
            throw new ForbiddenException("Cross-origin request rejected.");
        }

        var candidate = request.Headers[CsrfTokens.HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = await ReadBodyTokenAsync(request);
        }

        request.Cookies.TryGetValue(CsrfTokens.CookieName, out var cookie);
        var signingKey = context.HttpContext.RequestServices.GetService<AuthOptions>()?.IssuerSigningKey;

        if (!CsrfTokens.IsMatch(cookie, candidate, signingKey))
        {
            throw new ForbiddenException("CSRF token missing or invalid.");
        }
    }

    private static async Task<string?> ReadBodyTokenAsync(HttpRequest request)
    {
        if (request.ContentLength == 0
            || request.ContentType is null
            || !request.ContentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        request.EnableBuffering();
        request.Body.Position = 0;

        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if ((string.Equals(property.Name, CsrfTokens.BodyFieldName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(property.Name, CsrfTokens.BodyFieldAlias, StringComparison.OrdinalIgnoreCase))
                    && property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException)
        {
            return null;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }
}
