import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export interface OperatorShiftAssignmentResponse {
  id: string;
  operatorId: string;
  operatorIdentifier?: string | null;
  operatorName?: string | null;
  shiftId: string;
  shiftCode?: string | null;
  shiftName?: string | null;
  date: string;
  notes?: string | null;
}

export interface BrowseOperatorShiftAssignmentsRequest extends IPagedRequest {
  date?: string;
  dateFrom?: string;
  dateTo?: string;
  operatorId?: string;
  shiftId?: string;
}

export interface CreateOperatorShiftAssignmentRequest {
  operatorId: string;
  shiftId: string;
  date: string;
  notes?: string | null;
}

const BASE = '/api/operator-shift-assignments';

export const operatorShiftAssignmentService = {
  async browse(req: BrowseOperatorShiftAssignmentsRequest): Promise<IPagedResponse<OperatorShiftAssignmentResponse>> {
    const { data } = await http.get<IPagedResponse<OperatorShiftAssignmentResponse>>(BASE, {
      params: buildPagedParams(req)
    });
    return data;
  },
  async get(id: string): Promise<OperatorShiftAssignmentResponse> {
    const { data } = await http.get<OperatorShiftAssignmentResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateOperatorShiftAssignmentRequest): Promise<OperatorShiftAssignmentResponse> {
    const { data } = await http.post<OperatorShiftAssignmentResponse>(BASE, req);
    return data;
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
