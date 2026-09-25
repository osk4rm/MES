namespace AsistOff.MES.Integration.Tests.Infrastructure;

/// <summary>
/// Cookie helpers for the httpOnly auth transport (issue #241). TestServer does
/// not persist cookies automatically, so tests read <c>Set-Cookie</c> explicitly
/// and send a <c>Cookie</c> header — modelling a browser without giving
/// JavaScript access to the token values.
/// </summary>
public static class CookieTestHelpers
{
    public static IReadOnlyList<string> GetSetCookies(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return values.ToList();
        }

        return [];
    }

    public static string GetCookieValue(HttpResponseMessage response, string name)
    {
        foreach (var setCookie in GetSetCookies(response))
        {
            var first = setCookie.Split(';', 2)[0].Trim();
            var separator = first.IndexOf('=');
            if (separator > 0
                && string.Equals(first[..separator].Trim(), name, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(first[(separator + 1)..].Trim());
            }
        }

        throw new InvalidOperationException($"Set-Cookie for '{name}' not found.");
    }

    public static bool HasCookie(HttpResponseMessage response, string name)
    {
        try
        {
            GetCookieValue(response, name);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static void SetCookies(HttpClient client, params (string Name, string Value)[] cookies)
    {
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie",
            string.Join("; ", cookies.Select(c => $"{c.Name}={Uri.EscapeDataString(c.Value)}")));
    }
}
