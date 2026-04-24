import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export interface OperatorResponse {
  id: string;
  identifier: string;
  firstName: string;
  lastName: string;
  ratePerHour: number;
  department?: string | null;
}

export interface BrowseOperatorsRequest extends IPagedRequest {
  identifier?: string;
  firstName?: string;
  lastName?: string;
  ratePerHourFrom?: number;
  ratePerHourTo?: number;
  departmentId?: string;
}

export interface CreateOperatorRequest {
  identifier: string;
  firstName: string;
  lastName: string;
  ratePerHour: number;
  departmentId?: string | null;
  userId: string;
}

export interface UpdateOperatorRequest extends CreateOperatorRequest {
  id: string;
}

const BASE = '/api/operators';
const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

export const EMPTY_USER_ID = EMPTY_GUID;

export const operatorService = {
  async browse(req: BrowseOperatorsRequest): Promise<IPagedResponse<OperatorResponse>> {
    const { data } = await http.get<IPagedResponse<OperatorResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<OperatorResponse> {
    const { data } = await http.get<OperatorResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateOperatorRequest): Promise<OperatorResponse> {
    const { data } = await http.post<OperatorResponse>(BASE, req);
    return data;
  },
  async update(id: string, req: UpdateOperatorRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, req);
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
