import http from './http';
import type { IPagedResponse } from '../models/pagedModels';

export interface TenantSignupResponse {
  id: string;
  name: string;
  isActive: boolean;
}

export interface CreateTenantRequest {
  name: string;
  displayName: string;
  contactEmail: string;
  settings: string;
  password: string;
  confirmPassword: string;
}

export async function createTenant(request: CreateTenantRequest): Promise<TenantSignupResponse> {
  const response = await http.post<TenantSignupResponse>('/api/tenants', request);
  return response.data;
}

// Helpers shared by modules
export function buildPagedParams(req: object): Record<string, unknown> {
  const p: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(req as Record<string, unknown>)) {
    if (value === undefined || value === null || value === '') continue;
    if (Array.isArray(value)) {
      if (value.length) p[key] = value;
    } else {
      p[key] = value;
    }
  }
  return p;
}

export type { IPagedResponse };
