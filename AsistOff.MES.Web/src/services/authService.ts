
import type { SignInRequest, JwtResponse } from '../models/authModels';
import http from './http';
const AUTH_PATH = '/api/auth';

export async function signIn(data: SignInRequest): Promise<JwtResponse> {
  // Cookie transport (issue #242): the session arrives via httpOnly
  // Set-Cookie and the body token strings are intentionally empty.
  // Callers must not persist them anywhere — a 200 means the cookies
  // were issued.
  const response = await http.post(`${AUTH_PATH}/sign-in`, data);
  return response.data;
}

export async function signOut(): Promise<void> {
  // Empty body: the httpOnly refresh cookie selects the token server-side.
  await http.post(`${AUTH_PATH}/sign-out`, {});
}

/**
 * Reads the materialised permission grants from a sign-in/refresh response
 * body (issue #273). The backend serialises `Claims` camelCase with the
 * `permissions` key holding the grant list; anything unexpected yields an
 * empty (fail-closed) set instead of throwing.
 */
export function extractSessionPermissions(data: JwtResponse | null | undefined): string[] {
  const claims = data?.claims;
  if (!claims || typeof claims !== 'object') return [];
  const raw = claims['permissions'] ?? claims['Permissions'];
  const list = Array.isArray(raw) ? raw : typeof raw === 'string' ? [raw] : [];
  return [...new Set(list.filter((c): c is string => typeof c === 'string' && c.length > 0))];
}

export interface SessionRestoreResult {
  ok: boolean;
  permissions: string[];
  email?: string;
}

/**
 * Re-proves the cookie session after a reload (in-memory state is gone).
 * Succeeds whenever the refresh cookie is still valid — regardless of
 * role — and rotates the pair server-side. Also surfaces the permission
 * grants from the refresh response claims so the router permission guard
 * can enforce `meta.permission` without ever reading a token.
 */
export async function restoreSession(): Promise<SessionRestoreResult> {
  try {
    const { data } = await http.post<JwtResponse>(`${AUTH_PATH}/refresh`, {});
    const email = typeof data?.email === 'string' && data.email.length > 0 ? data.email : undefined;
    return { ok: true, permissions: extractSessionPermissions(data), email };
  } catch {
    return { ok: false, permissions: [] };
  }
}

/**
 * Boolean convenience over restoreSession for callers that only need the
 * session verdict (router guard, specs). Returns false instead of
 * throwing so the router guard can bounce to login with a redirect param.
 */
export async function refreshSession(): Promise<boolean> {
  const restored = await restoreSession();
  return restored.ok;
}
