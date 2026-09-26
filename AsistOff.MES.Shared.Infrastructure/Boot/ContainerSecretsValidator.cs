using System.Text;

namespace AsistOff.MES.Shared.Infrastructure.Boot;

/// <summary>
/// Fail-fast guard for container runtime secrets (issue #271). Production
/// refuses to boot with a missing or weak <c>POSTGRES_PASSWORD</c> or
/// <c>auth:IssuerSigningKey</c> instead of silently running with the
/// historical <c>root</c> / empty defaults. Outside Production this is a
/// no-op so local development and the integration-test host (which uses its
/// own Testcontainers credentials) keep working unchanged.
/// </summary>
public static class ContainerSecretsValidator
{
    /// <summary>
    /// Minimum accepted PostgreSQL password length. Generated secrets
    /// (<c>openssl rand -base64 32</c>, 44 chars) pass comfortably; short
    /// human defaults do not.
    /// </summary>
    public const int MinimumPostgresPasswordLength = 16;

    /// <summary>
    /// Minimum signing-key size for HMAC-SHA256: 32 bytes (256 bits).
    /// Mirrors <see cref="Auth.AuthOptionsValidator.MinimumKeyBytes"/>.
    /// </summary>
    public const int MinimumSigningKeyBytes = 32;

    private static readonly string[] WeakPostgresPasswords =
    [
        "root", "password", "admin", "mes", "postgres", "postgres123",
        "123456", "changeme", "test", "letmein",
    ];

    /// <summary>
    /// Validates both secrets when <paramref name="isProduction"/> is
    /// <c>true</c>; returns silently otherwise.
    /// </summary>
    public static void Validate(string? postgresPassword, string? issuerSigningKey, bool isProduction)
    {
        if (!isProduction)
        {
            return;
        }

        ValidatePostgresPassword(postgresPassword);
        ValidateIssuerSigningKey(issuerSigningKey);
    }

    /// <summary>
    /// Rejects a missing, too-short, or well-known-default PostgreSQL password.
    /// </summary>
    public static void ValidatePostgresPassword(string? postgresPassword)
    {
        if (string.IsNullOrWhiteSpace(postgresPassword))
        {
            throw new InvalidOperationException(
                "POSTGRES_PASSWORD is required but was not set. " +
                "Copy .env.example to .env and generate one with `openssl rand -base64 32` " +
                "(or run scripts/generate-env.sh). The stack refuses to boot with a default password.");
        }

        var password = postgresPassword.Trim();
        if (password.Length < MinimumPostgresPasswordLength
            || WeakPostgresPasswords.Contains(password, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"POSTGRES_PASSWORD is too weak: it must be at least {MinimumPostgresPasswordLength} characters " +
                "and must not be a well-known default such as 'root'. " +
                "Generate one with `openssl rand -base64 32` (or run scripts/generate-env.sh).");
        }
    }

    /// <summary>
    /// Rejects a missing or too-short JWT signing key, naming the setting the
    /// same way <see cref="Auth.AuthOptionsValidator"/> does so operators get
    /// one consistent message whichever guard fires first.
    /// </summary>
    public static void ValidateIssuerSigningKey(string? issuerSigningKey)
    {
        if (string.IsNullOrWhiteSpace(issuerSigningKey))
        {
            throw new InvalidOperationException(
                "auth:IssuerSigningKey must be set via User Secrets or an environment variable before the application starts.");
        }

        var keyBytes = Encoding.UTF8.GetByteCount(issuerSigningKey);
        if (keyBytes < MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"auth:IssuerSigningKey must be at least {MinimumSigningKeyBytes} bytes (256 bits) for HMAC-SHA256; " +
                $"current key is {keyBytes} bytes. Provide a longer secret via User Secrets or an environment variable.");
        }
    }
}
