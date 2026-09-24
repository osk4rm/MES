import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export const AndonSignalCategory = {
  Downtime: 1,
  Quality: 2,
  Material: 3,
  Other: 4
} as const;
export type AndonSignalCategory = typeof AndonSignalCategory[keyof typeof AndonSignalCategory];

export const AndonSignalStatus = {
  Active: 1,
  Acknowledged: 2,
  Resolved: 3
} as const;
export type AndonSignalStatus = typeof AndonSignalStatus[keyof typeof AndonSignalStatus];

export interface AndonSignalResponse {
  id: string;
  machineId: string;
  category: AndonSignalCategory;
  reasonCodeId?: string | null;
  status: AndonSignalStatus;
  raisedAt: string;
  acknowledgedAt?: string | null;
  resolvedAt?: string | null;
  notes?: string | null;
  raisedByOperatorId?: string | null;
  productionOrderId?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface BrowseAndonSignalsRequest extends IPagedRequest {
  machineId?: string;
  category?: AndonSignalCategory;
  status?: AndonSignalStatus;
  raisedFrom?: string;
  raisedTo?: string;
}

export interface RaiseAndonSignalRequest {
  machineId: string;
  category: AndonSignalCategory;
  reasonCodeId?: string | null;
  raisedAt: string;
  notes?: string | null;
  raisedByOperatorId?: string | null;
  productionOrderId?: string | null;
}

export interface UpdateAndonSignalRequest {
  id: string;
  category: AndonSignalCategory;
  reasonCodeId?: string | null;
  notes?: string | null;
}

export interface ResolveAndonSignalRequest {
  resolvedAt?: string | null;
}

const BASE = '/api/andon-signals';

export const andonSignalService = {
  async browse(req: BrowseAndonSignalsRequest): Promise<IPagedResponse<AndonSignalResponse>> {
    const { data } = await http.get<IPagedResponse<AndonSignalResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<AndonSignalResponse> {
    const { data } = await http.get<AndonSignalResponse>(`${BASE}/${id}`);
    return data;
  },
  async raise(req: RaiseAndonSignalRequest): Promise<AndonSignalResponse> {
    const { data } = await http.post<AndonSignalResponse>(BASE, req);
    return data;
  },
  async acknowledge(id: string): Promise<AndonSignalResponse> {
    const { data } = await http.post<AndonSignalResponse>(`${BASE}/${id}/acknowledge`, {});
    return data;
  },
  async resolve(id: string, req: ResolveAndonSignalRequest): Promise<AndonSignalResponse> {
    const { data } = await http.post<AndonSignalResponse>(`${BASE}/${id}/resolve`, req);
    return data;
  },
  async update(id: string, req: UpdateAndonSignalRequest): Promise<AndonSignalResponse> {
    const { data } = await http.put<AndonSignalResponse>(`${BASE}/${id}`, req);
    return data;
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
