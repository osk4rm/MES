using System.Text;

namespace AsistOff.MES.Shared.Infrastructure.Auth;

/// <summary>
/// Startup guard for JWT configuration. Enforces the hardening rules from
/// the auth tracker gap: a minimum 256-bit HMAC signing key at all times,
/// and mandatory issuer/audience validation in production with a documented
/// dev-only bypass (Development/Test/Staging may run without audience).
/// </summary>
public static class AuthOptionsValidator
{
    /// <summary>Minimum signing-key size for HMAC-SHA256: 32 bytes (256 bits).</summary>
    public const int MinimumKeyBytes = 32;

    public static void Validate(AuthOptions options, bool isProduction)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.IssuerSigningKey))
        {
            throw new InvalidOperationException(
                "auth:IssuerSigningKey must be set via User Secrets or an environment variable before the application starts.");
        }

        var keyBytes = Encoding.UTF8.GetByteCount(options.IssuerSigningKey);
        if (keyBytes < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"auth:IssuerSigningKey must be at least {MinimumKeyBytes} bytes (256 bits) for HMAC-SHA256; " +
                $"current key is {keyBytes} bytes. Provide a longer secret via User Secrets or an environment variable.");
        }

        if (!isProduction)
        {
            return;
        }

        var hasIssuer = !string.IsNullOrWhiteSpace(options.Issuer)
            || !string.IsNullOrWhiteSpace(options.ValidIssuer)
            || (options.ValidIssuers?.Any(v => !string.IsNullOrWhiteSpace(v)) is true);
        if (!hasIssuer)
        {
            throw new InvalidOperationException(
                "auth:Issuer (or auth:ValidIssuer / auth:ValidIssuers) must be configured in a Production environment. " +
                "JWT issuer validation cannot run without a known issuer.");
        }

        var hasAudience = !string.IsNullOrWhiteSpace(options.Audience)
            || !string.IsNullOrWhiteSpace(options.ValidAudience)
            || (options.ValidAudiences?.Any(v => !string.IsNullOrWhiteSpace(v)) is true);
        if (!hasAudience)
        {
            throw new InvalidOperationException(
                "auth:Audience (or auth:ValidAudience / auth:ValidAudiences) must be configured in a Production environment. " +
                "JWT audience validation cannot run without a known audience. " +
                "Dev-only bypass: audience validation may be disabled outside Production.");
        }

        if (!options.ValidateIssuer)
        {
            throw new InvalidOperationException(
                "auth:ValidateIssuer must be true in a Production environment. " +
                "Disabling issuer validation is a dev-only bypass.");
        }

        if (!options.ValidateAudience || !options.RequireAudience)
        {
            throw new InvalidOperationException(
                "auth:ValidateAudience and auth:RequireAudience must be true in a Production environment. " +
                "Disabling audience validation is a dev-only bypass.");
        }

        if (!options.ValidateLifetime || !options.RequireExpirationTime || !options.RequireSignedTokens)
        {
            throw new InvalidOperationException(
                "auth:ValidateLifetime, auth:RequireExpirationTime and auth:RequireSignedTokens must be true in a Production environment.");
        }

        if (!options.ValidateIssuerSigningKey)
        {
            throw new InvalidOperationException(
                "auth:ValidateIssuerSigningKey must be true in a Production environment.");
        }
    }
}
