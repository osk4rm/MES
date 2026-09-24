import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export const LotStatus = {
  Available: 1,
  OnHold: 2,
  Consumed: 3,
  Scrapped: 4,
  Expired: 5
} as const;
export type LotStatus = typeof LotStatus[keyof typeof LotStatus];

export interface LotResponse {
  id: string;
  code: string;
  productId: string;
  measureUnitId: string;
  quantity: number;
  status: LotStatus;
  supplierLotNumber?: string | null;
  producedAt?: string | null;
  expiryDate?: string | null;
  notes?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface BrowseLotsRequest extends IPagedRequest {
  code?: string;
  productId?: string;
  status?: LotStatus;
  expiryFrom?: string;
  expiryTo?: string;
}

export interface CreateLotRequest {
  code: string;
  productId: string;
  measureUnitId: string;
  quantity: number;
  supplierLotNumber?: string | null;
  producedAt?: string | null;
  expiryDate?: string | null;
  notes?: string | null;
}

export interface UpdateLotRequest extends CreateLotRequest {
  id: string;
}

const BASE = '/api/lots';

export const lotService = {
  async browse(req: BrowseLotsRequest): Promise<IPagedResponse<LotResponse>> {
    const { data } = await http.get<IPagedResponse<LotResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<LotResponse> {
    const { data } = await http.get<LotResponse>(`${BASE}/${id}`);
    return data;
  },
  async getByCode(code: string): Promise<LotResponse> {
    const { data } = await http.get<LotResponse>(`${BASE}/by-code/${encodeURIComponent(code)}`);
    return data;
  },
  async create(req: CreateLotRequest): Promise<LotResponse> {
    const { data } = await http.post<LotResponse>(BASE, req);
    return data;
  },
  async update(id: string, req: UpdateLotRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, req);
  },
  async changeStatus(id: string, status: LotStatus): Promise<void> {
    await http.post(`${BASE}/${id}/status`, { status });
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
