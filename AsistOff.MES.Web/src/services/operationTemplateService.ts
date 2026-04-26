import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export const RunTimeMode = {
  PerUnitSeconds: 1,
  PerBatchMinutes: 2
} as const;
export type RunTimeMode = typeof RunTimeMode[keyof typeof RunTimeMode];

export interface OperationTemplateResponse {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  operationType?: string | null;
  isActive: boolean;
  setupTimeMinutes?: number | null;
  runTimeMode: RunTimeMode;
  runTimePerUnitSeconds?: number | null;
  runTimePerBatchMinutes?: number | null;
  teardownTimeMinutes?: number | null;
  queueTimeMinutes?: number | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface BrowseOperationTemplatesRequest extends IPagedRequest {
  name?: string;
  code?: string;
  isActive?: boolean;
}

export interface CreateOperationTemplateRequest {
  code: string;
  name: string;
  description?: string | null;
  operationType?: string | null;
  isActive: boolean;
  setupTimeMinutes?: number | null;
  runTimeMode: RunTimeMode;
  runTimePerUnitSeconds?: number | null;
  runTimePerBatchMinutes?: number | null;
  teardownTimeMinutes?: number | null;
  queueTimeMinutes?: number | null;
}

export interface UpdateOperationTemplateRequest extends CreateOperationTemplateRequest {
  id: string;
}

const BASE = '/api/operation-templates';

export const operationTemplateService = {
  async browse(req: BrowseOperationTemplatesRequest): Promise<IPagedResponse<OperationTemplateResponse>> {
    const { data } = await http.get<IPagedResponse<OperationTemplateResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<OperationTemplateResponse> {
    const { data } = await http.get<OperationTemplateResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateOperationTemplateRequest): Promise<OperationTemplateResponse> {
    const { data } = await http.post<OperationTemplateResponse>(BASE, req);
    return data;
  },
  async update(id: string, req: UpdateOperationTemplateRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, req);
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
