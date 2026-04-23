import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export interface WarehouseResponse {
  id: string;
  name: string;
  syncId?: string | null;
}

export interface BrowseWarehousesRequest extends IPagedRequest {
  name?: string;
}

export interface CreateWarehouseRequest {
  name: string;
  syncId?: string | null;
}

export interface UpdateWarehouseRequest {
  id: string;
  name: string;
}

const BASE = '/api/warehouses';

export const warehouseService = {
  async browse(req: BrowseWarehousesRequest): Promise<IPagedResponse<WarehouseResponse>> {
    const { data } = await http.get<IPagedResponse<WarehouseResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<WarehouseResponse> {
    const { data } = await http.get<WarehouseResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateWarehouseRequest): Promise<WarehouseResponse> {
    const { data } = await http.post<WarehouseResponse>(BASE, req);
    return data;
  },
  async update(id: string, req: UpdateWarehouseRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, req);
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
