import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export interface SpcMeasurementResponse {
  id: string;
  characteristicId: string;
  value: number;
  measuredAt: string;
  notes?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface BrowseSpcMeasurementsRequest extends IPagedRequest {
  characteristicId?: string;
  from?: string;
  to?: string;
}

export interface GetSpcMeasurementChartQuery {
  characteristicId: string;
  from?: string;
  to?: string;
}

export interface SpcMeasurementChartPoint {
  id: string;
  value: number;
  measuredAt: string;
  isOutOfControl: boolean;
  isOutOfSpec: boolean;
}

export interface SpcMeasurementChartResponse {
  characteristicId: string;
  nominalValue?: number | null;
  lowerSpecLimit?: number | null;
  upperSpecLimit?: number | null;
  lowerControlLimit?: number | null;
  upperControlLimit?: number | null;
  points: SpcMeasurementChartPoint[];
  totalCount: number;
  outOfControlCount: number;
  outOfSpecCount: number;
}

const BASE = '/api/spc-measurements';

export const spcMeasurementService = {
  async browse(req: BrowseSpcMeasurementsRequest): Promise<IPagedResponse<SpcMeasurementResponse>> {
    const { data } = await http.get<IPagedResponse<SpcMeasurementResponse>>(BASE, {
      params: buildPagedParams(req)
    });
    return data;
  },
  async getChart(query: GetSpcMeasurementChartQuery): Promise<SpcMeasurementChartResponse> {
    const { data } = await http.get<SpcMeasurementChartResponse>(`${BASE}/chart`, {
      params: buildPagedParams(query)
    });
    return data;
  },
  async get(id: string): Promise<SpcMeasurementResponse> {
    const { data } = await http.get<SpcMeasurementResponse>(`${BASE}/${encodeURIComponent(id)}`);
    return data;
  }
};
