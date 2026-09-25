/**
 * Safe post-login redirect resolution (issue #241 follow-up).
 *
 * The `?redirect` query value is attacker-controlled (it comes from the URL),
 * so only single-slash absolute paths are accepted. Protocol-relative URLs
 * (`//evil.example`) and backslash variants (`/\\evil`, `/foo\bar`) fall back
 * to `/dashboard`.
 */
export const fallbackRedirect = '/dashboard';

export function resolveSafeRedirect(redirect: unknown): string {
  if (typeof redirect !== 'string' || redirect.length === 0) {
    return fallbackRedirect;
  }
  if (!redirect.startsWith('/')) {
    return fallbackRedirect;
  }
  if (redirect.startsWith('//')) {
    return fallbackRedirect;
  }
  if (redirect.includes('\\')) {
    return fallbackRedirect;
  }
  return redirect;
}
