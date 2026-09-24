import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export const SpcChartType = {
  XbarR: 1,
  XbarS: 2,
  XmR: 3,
  PChart: 4,
  CChart: 5
} as const;
export type SpcChartType = typeof SpcChartType[keyof typeof SpcChartType];

export interface SpcCharacteristicResponse {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  productId?: string | null;
  machineId?: string | null;
  chartType: SpcChartType;
  nominalValue?: number | null;
  lowerSpecLimit?: number | null;
  upperSpecLimit?: number | null;
  lowerControlLimit?: number | null;
  upperControlLimit?: number | null;
  sampleSize: number;
  unit?: string | null;
  isActive: boolean;
}

export interface BrowseSpcCharacteristicsRequest extends IPagedRequest {
  search?: string;
  productId?: string;
  machineId?: string;
  isActive?: boolean;
}

export interface CreateSpcCharacteristicRequest {
  code: string;
  name: string;
  description?: string | null;
  productId?: string | null;
  machineId?: string | null;
  chartType: SpcChartType;
  nominalValue?: number | null;
  lowerSpecLimit?: number | null;
  upperSpecLimit?: number | null;
  lowerControlLimit?: number | null;
  upperControlLimit?: number | null;
  sampleSize: number;
  unit?: string | null;
  isActive: boolean;
}

export interface UpdateSpcCharacteristicRequest extends Omit<CreateSpcCharacteristicRequest, 'code'> {
  id: string;
}

const BASE = '/api/spc-characteristics';

export const spcCharacteristicService = {
  async browse(req: BrowseSpcCharacteristicsRequest): Promise<IPagedResponse<SpcCharacteristicResponse>> {
    const { data } = await http.get<IPagedResponse<SpcCharacteristicResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<SpcCharacteristicResponse> {
    const { data } = await http.get<SpcCharacteristicResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateSpcCharacteristicRequest): Promise<SpcCharacteristicResponse> {
    const { data } = await http.post<SpcCharacteristicResponse>(BASE, req);
    return data;
  },
  async update(id: string, req: UpdateSpcCharacteristicRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, req);
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
