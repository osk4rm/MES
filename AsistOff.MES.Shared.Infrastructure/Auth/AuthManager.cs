using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Providers;
using Microsoft.IdentityModel.Tokens;

namespace AsistOff.MES.Shared.Infrastructure.Auth;

public class AuthManager : IAuthManager
{
    private static readonly Dictionary<string, IEnumerable<string>> EmptyClaims = new();
    private readonly AuthOptions _options;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly SigningCredentials _signingCredentials;
    private readonly string _issuer;

    public AuthManager(AuthOptions options, IDateTimeProvider dateTimeProvider)
    {
        var issuerSigningKey = options.IssuerSigningKey;
        if (string.IsNullOrWhiteSpace(issuerSigningKey))
        {
            throw new InvalidOperationException(
                "auth:IssuerSigningKey must be set via User Secrets or an environment variable before the application starts.");
        }

        var keyBytes = Encoding.UTF8.GetByteCount(issuerSigningKey);
        if (keyBytes < AuthOptionsValidator.MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"auth:IssuerSigningKey must be at least {AuthOptionsValidator.MinimumKeyBytes} bytes (256 bits) for HMAC-SHA256; " +
                $"current key is {keyBytes} bytes. Provide a longer secret via User Secrets or an environment variable.");
        }

        _options = options;
        _dateTimeProvider = dateTimeProvider;
        _signingCredentials =
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(issuerSigningKey)),
                SecurityAlgorithms.HmacSha256);
        _issuer = options.Issuer ?? "AsistOff.MES";
    }

    public JsonWebToken CreateToken(string userId, string? role = null, string? audience = null,
        IDictionary<string, IEnumerable<string>>? claims = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID claim (subject) cannot be empty.", nameof(userId));
        }

        var now = _dateTimeProvider.UtcNow;
        var jwtClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.UniqueName, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeMilliseconds().ToString())
        };

        if (!string.IsNullOrWhiteSpace(role))
        {
            jwtClaims.Add(new Claim(ClaimTypes.Role, role));
        }

        var tokenAudience = audience ?? _options.Audience;
        if (!string.IsNullOrWhiteSpace(tokenAudience))
        {
            jwtClaims.Add(new Claim(JwtRegisteredClaimNames.Aud, tokenAudience));
        }

        if (claims?.Any() is true)
        {
            var customClaims = new List<Claim>();
            foreach (var (claim, values) in claims)
            {
                customClaims.AddRange(values.Select(value => new Claim(claim, value)));
            }

            jwtClaims.AddRange(customClaims);
        }

        var expires = now.Add(GetAccessLifetime(_options));
        var jwt = new JwtSecurityToken(
            issuer: _issuer,
            audience: tokenAudience,
            claims: jwtClaims,
            notBefore: now,
            expires: expires,
            signingCredentials: _signingCredentials
        );

        var token = new JwtSecurityTokenHandler().WriteToken(jwt);

        return new JsonWebToken
        {
            AccessToken = token,
            RefreshToken = string.Empty,
            Expires = new DateTimeOffset(expires).ToUnixTimeMilliseconds(),
            Id = userId,
            Role = role ?? string.Empty,
            Claims = claims ?? EmptyClaims
        };
    }

    public static TimeSpan GetAccessLifetime(AuthOptions options)
        => options.Expiry != TimeSpan.Zero ? options.Expiry : options.AccessTokenLifetime;
}
