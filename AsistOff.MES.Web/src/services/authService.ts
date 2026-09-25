
import type { SignInRequest, JwtResponse } from '../models/authModels';
import http from './http';
const AUTH_PATH = '/api/auth';

export async function signIn(data: SignInRequest): Promise<JwtResponse> {
  const response = await http.post(`${AUTH_PATH}/sign-in`, data);
  return response.data;
}

export async function signOut(): Promise<void> {
  // Empty body: the httpOnly refresh cookie selects the token server-side.
  await http.post(`${AUTH_PATH}/sign-out`, {});
}
