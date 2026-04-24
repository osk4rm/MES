namespace AsistOff.MES.Shared.Abstractions.Auth;

public class JsonWebToken
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public long Expires { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Email { get; set; }
    public IDictionary<string, IEnumerable<string>> Claims { get; set; } = new Dictionary<string, IEnumerable<string>>();
}