import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export const ReasonCodeCategory = {
  Downtime: 1,
  Scrap: 2,
  Quality: 3,
  Setup: 4,
  Other: 5
} as const;
export type ReasonCodeCategory = typeof ReasonCodeCategory[keyof typeof ReasonCodeCategory];

export interface ReasonCodeResponse {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  category: ReasonCodeCategory;
  isActive: boolean;
  sortIndex: number;
}

export interface BrowseReasonCodesRequest extends IPagedRequest {
  code?: string;
  name?: string;
  category?: ReasonCodeCategory;
  isActive?: boolean;
}

export interface CreateReasonCodeRequest {
  code: string;
  name: string;
  description?: string | null;
  category: ReasonCodeCategory;
  isActive: boolean;
  sortIndex: number;
}

export interface UpdateReasonCodeRequest extends CreateReasonCodeRequest {
  id: string;
}

const BASE = '/api/reason-codes';

export const reasonCodeService = {
  async browse(req: BrowseReasonCodesRequest): Promise<IPagedResponse<ReasonCodeResponse>> {
    const { data } = await http.get<IPagedResponse<ReasonCodeResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<ReasonCodeResponse> {
    const { data } = await http.get<ReasonCodeResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateReasonCodeRequest): Promise<ReasonCodeResponse> {
    const { data } = await http.post<ReasonCodeResponse>(BASE, req);
    return data;
  },
  async update(id: string, req: UpdateReasonCodeRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, req);
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
