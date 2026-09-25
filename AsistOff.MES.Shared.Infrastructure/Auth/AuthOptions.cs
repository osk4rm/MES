namespace AsistOff.MES.Shared.Infrastructure.Auth;

public class AuthOptions
{
    public bool AuthenticationDisabled { get; set; }
    public string? Issuer { get; set; }
    public string? IssuerSigningKey { get; set; }
    public string? Authority { get; set; }
    public string? Audience { get; set; }
    public string Challenge { get; set; } = "Bearer";
    public string? MetadataAddress { get; set; }
    public bool SaveToken { get; set; } = true;
    public bool SaveSigninToken { get; set; }
    public bool RequireAudience { get; set; } = true;
    public bool RequireHttpsMetadata { get; set; } = true;
    public bool RequireExpirationTime { get; set; } = true;
    public bool RequireSignedTokens { get; set; } = true;
    public TimeSpan Expiry { get; set; }
    /// <summary>
    /// Lifetime of the short-lived JWT access token. When <see cref="Expiry"/>
    /// is set (legacy <c>auth:Expiry</c> configuration) it takes precedence
    /// for backwards compatibility; otherwise this value applies.
    /// Default is 15 minutes.
    /// </summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
    /// <summary>
    /// Lifetime of the opaque server-side refresh token. Default is 7 days.
    /// </summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);
    public string? ValidAudience { get; set; }
    public IEnumerable<string>? ValidAudiences { get; set; }
    public string? ValidIssuer { get; set; }
    public IEnumerable<string>? ValidIssuers { get; set; }
    public bool ValidateActor { get; set; }
    public bool ValidateAudience { get; set; } = true;
    public bool ValidateIssuer { get; set; } = true;
    public bool ValidateLifetime { get; set; } = true;
    public bool ValidateTokenReplay { get; set; }
    public bool ValidateIssuerSigningKey { get; set; }
    public bool RefreshOnIssuerKeyNotFound { get; set; } = true;
    public bool IncludeErrorDetails { get; set; } = false;
    public string? AuthenticationType { get; set; }
    public string? NameClaimType { get; set; }
    public string? RoleClaimType { get; set; }
}