
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
 * Re-proves the cookie session after a reload (in-memory state is gone).
 * Succeeds whenever the refresh cookie is still valid — regardless of
 * role — and rotates the pair server-side. Returns false instead of
 * throwing so the router guard can bounce to login with a redirect param.
 */
export async function refreshSession(): Promise<boolean> {
  try {
    await http.post(`${AUTH_PATH}/refresh`, {});
    return true;
  } catch {
    return false;
  }
}
