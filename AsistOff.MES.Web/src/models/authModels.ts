export interface SignInRequest {
  email: string;
  password: string;
}

/**
 * Permission codes mirrored from the backend
 * `AsistOff.MES.Users.Core.Rbac.RbacDefaults` (referenced, not redefined —
 * no new permission strings are introduced on the frontend).
 */
export const Permissions = {
  ProductionWrite: 'production.write',
  ConfigurationWrite: 'configuration.write',
  TenantAdmin: 'tenant.admin'
} as const;
export type PermissionCode = typeof Permissions[keyof typeof Permissions];

export interface JwtResponse {
    // Cookie transport (issue #242): the backend always returns empty
    // token strings here — the session lives in httpOnly cookies.
    // Never persist these values; they are kept only for contract shape.
    accessToken: string;
    refreshToken: string;
    expires: number;
    // Issue #273: sign-in/refresh bodies also carry the materialised JWT
    // claims (permissions, email, role) next to the emptied tokens, so the
    // route permission guard can read grants without touching a token.
    // Serialized camelCase by ASP.NET (`claims`); read defensively.
    claims?: Record<string, string[] | string>;
    role?: string;
    email?: string;
}
