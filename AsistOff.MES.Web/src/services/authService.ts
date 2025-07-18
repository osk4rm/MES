
import type { SignInRequest, JwtResponse } from '../models/authModels';
import http from './http';
const AUTH_PATH = '/api/auth';

export async function signIn(data: SignInRequest): Promise<JwtResponse> {
  const response = await http.post(`${AUTH_PATH}/sign-in`, data);
  return response.data;
}
