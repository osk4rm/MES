import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export interface MachineResponse {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  departmentId?: string | null;
  isActive: boolean;
}

export interface BrowseMachinesRequest extends IPagedRequest {
  name?: string;
  code?: string;
  isActive?: boolean;
  departmentId?: string;
}

export interface CreateMachineRequest {
  code: string;
  name: string;
  description?: string | null;
  departmentId?: string | null;
  isActive: boolean;
}

export interface UpdateMachineRequest extends CreateMachineRequest {
  id: string;
}

const BASE = '/api/machines';

export const machineService = {
  async browse(req: BrowseMachinesRequest): Promise<IPagedResponse<MachineResponse>> {
    const { data } = await http.get<IPagedResponse<MachineResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<MachineResponse> {
    const { data } = await http.get<MachineResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateMachineRequest): Promise<MachineResponse> {
    const { data } = await http.post<MachineResponse>(BASE, req);
    return data;
  },
  async update(id: string, req: UpdateMachineRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, req);
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
