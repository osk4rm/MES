using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AsistOff.MES.Users.Application.Features.Authentication.Refresh;

/// <summary>
/// Opaque refresh-token minting and hashing. The opaque value is 256 bits
/// of randomness (Base64Url); only its SHA-256 hex hash is persisted.
/// </summary>
public static class RefreshTokenHasher
{
    public static (string Token, string Hash) Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Base64UrlEncoder.Encode(bytes);
        return (token, Hash(token));
    }

    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
