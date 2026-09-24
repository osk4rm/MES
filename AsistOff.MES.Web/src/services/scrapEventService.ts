import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export interface ScrapEventResponse {
  id: string;
  machineId: string;
  reasonCodeId: string;
  quantity: number;
  reportedAt: string;
  notes?: string | null;
  reportedByOperatorId?: string | null;
  productionOrderId?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface BrowseScrapEventsRequest extends IPagedRequest {
  machineId?: string;
  reasonCodeId?: string;
  reportedFrom?: string;
  reportedTo?: string;
}

export interface CreateScrapEventRequest {
  machineId: string;
  reasonCodeId: string;
  quantity: number;
  reportedAt: string;
  notes?: string | null;
  reportedByOperatorId?: string | null;
  productionOrderId?: string | null;
}

export interface UpdateScrapEventRequest {
  id: string;
  reasonCodeId: string;
  quantity: number;
  notes?: string | null;
}

const BASE = '/api/scrap-events';

export const scrapEventService = {
  async browse(req: BrowseScrapEventsRequest): Promise<IPagedResponse<ScrapEventResponse>> {
    const { data } = await http.get<IPagedResponse<ScrapEventResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<ScrapEventResponse> {
    const { data } = await http.get<ScrapEventResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateScrapEventRequest): Promise<ScrapEventResponse> {
    const { data } = await http.post<ScrapEventResponse>(BASE, req);
    return data;
  },
  async update(id: string, req: UpdateScrapEventRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, req);
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
