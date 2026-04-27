using System.Net.Http.Json;
using System.Text.Json;

namespace AsistOff.MES.IntegrationTests.Infrastructure;

/// <summary>
/// HttpClient extensions that make integration test bodies short and assert‑heavy
/// without sprinkling boilerplate (status check, JSON deserialization options).
/// </summary>
internal static class HttpClientExtensions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<T>(Json);
        return body!;
    }

    public static async Task<T> PostJsonAsync<T>(this HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body, Json);
        await EnsureSuccessOrThrow(response, $"POST {url}");
        var result = await response.Content.ReadFromJsonAsync<T>(Json);
        return result!;
    }

    public static async Task PostJsonAsync(this HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body, Json);
        await EnsureSuccessOrThrow(response, $"POST {url}");
    }

    public static async Task PutJsonAsync(this HttpClient client, string url, object body)
    {
        var response = await client.PutAsJsonAsync(url, body, Json);
        await EnsureSuccessOrThrow(response, $"PUT {url}");
    }

    private static async Task EnsureSuccessOrThrow(HttpResponseMessage response, string context)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException($"{context} -> {(int)response.StatusCode} {response.StatusCode}\n{body}");
    }
}
