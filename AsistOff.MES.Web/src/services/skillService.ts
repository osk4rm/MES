import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export interface SkillResponse {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface BrowseSkillsRequest extends IPagedRequest {
  name?: string;
  code?: string;
  isActive?: boolean;
}

export interface CreateSkillRequest {
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface UpdateSkillRequest extends CreateSkillRequest {
  id: string;
}

const BASE = '/api/skills';

export const skillService = {
  async browse(req: BrowseSkillsRequest): Promise<IPagedResponse<SkillResponse>> {
    const { data } = await http.get<IPagedResponse<SkillResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<SkillResponse> {
    const { data } = await http.get<SkillResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateSkillRequest): Promise<SkillResponse> {
    const { data } = await http.post<SkillResponse>(BASE, req);
    return data;
  },
  async update(id: string, req: UpdateSkillRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, req);
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
