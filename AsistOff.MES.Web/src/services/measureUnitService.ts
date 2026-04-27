import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export const MeasureUnitType = {
  Product: 1
} as const;
export type MeasureUnitType = typeof MeasureUnitType[keyof typeof MeasureUnitType];

export interface MeasureUnitResponse {
  id: string;
  name: string;
  symbol: string;
  type: MeasureUnitType;
  conversionFactor?: number | null;
  baseUnitId?: string | null;
  baseUnitName?: string | null;
  isActive: boolean;
  description?: string | null;
  syncId?: string | null;
}

export interface BrowseMeasureUnitsRequest extends IPagedRequest {
  searchTerm?: string;
  type?: MeasureUnitType;
  isActive?: boolean;
}

export interface CreateMeasureUnitRequest {
  name: string;
  symbol: string;
  type: MeasureUnitType;
  conversionFactor?: number | null;
  baseUnitId?: string | null;
  isActive: boolean;
  description?: string | null;
  syncId?: string | null;
}

export interface UpdateMeasureUnitRequest extends CreateMeasureUnitRequest {
  id: string;
}

const BASE = '/api/measure-units';

export const measureUnitService = {
  async browse(req: BrowseMeasureUnitsRequest): Promise<IPagedResponse<MeasureUnitResponse>> {
    const { data } = await http.get<IPagedResponse<MeasureUnitResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<MeasureUnitResponse> {
    const { data } = await http.get<MeasureUnitResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateMeasureUnitRequest): Promise<MeasureUnitResponse> {
    const { data } = await http.post<MeasureUnitResponse>(BASE, req);
    return data;
  },
  async update(id: string, req: UpdateMeasureUnitRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, req);
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
