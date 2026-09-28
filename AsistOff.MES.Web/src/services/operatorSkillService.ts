import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export interface OperatorSkillQualificationResponse {
  id: string;
  operatorId: string;
  operatorIdentifier?: string | null;
  operatorName?: string | null;
  skillId: string;
  skillCode?: string | null;
  skillName?: string | null;
}

export interface BrowseOperatorSkillsRequest extends IPagedRequest {
  operatorId?: string;
  skillId?: string;
}

export interface AssignOperatorSkillRequest {
  operatorId: string;
  skillId: string;
}

const BASE = '/api/operator-skills';

export const operatorSkillService = {
  async browse(req: BrowseOperatorSkillsRequest): Promise<IPagedResponse<OperatorSkillQualificationResponse>> {
    const { data } = await http.get<IPagedResponse<OperatorSkillQualificationResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<OperatorSkillQualificationResponse> {
    const { data } = await http.get<OperatorSkillQualificationResponse>(`${BASE}/${id}`);
    return data;
  },
  async assign(req: AssignOperatorSkillRequest): Promise<OperatorSkillQualificationResponse> {
    const { data } = await http.post<OperatorSkillQualificationResponse>(BASE, req);
    return data;
  },
  async unassign(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
