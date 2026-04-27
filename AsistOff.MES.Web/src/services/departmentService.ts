import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export interface DepartmentResponse {
  id: string;
  code: string;
  name: string;
}

export interface BrowseDepartmentsRequest extends IPagedRequest {
  code?: string;
  name?: string;
}

export interface CreateDepartmentRequest {
  code: string;
  name: string;
}

export interface UpdateDepartmentRequest extends CreateDepartmentRequest {
  id: string;
}

const BASE = '/api/departments';

export const departmentService = {
  async browse(req: BrowseDepartmentsRequest): Promise<IPagedResponse<DepartmentResponse>> {
    const { data } = await http.get<IPagedResponse<DepartmentResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<DepartmentResponse> {
    const { data } = await http.get<DepartmentResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateDepartmentRequest): Promise<DepartmentResponse> {
    const { data } = await http.post<DepartmentResponse>(BASE, req);
    return data;
  },
  async update(id: string, req: UpdateDepartmentRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, req);
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
