import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';
import type { MaintenanceWorkOrderResponse } from './maintenanceWorkOrderService';

export const MaintenancePlanTriggerType = {
  Time: 1,
  Meter: 2
} as const;
export type MaintenancePlanTriggerType = typeof MaintenancePlanTriggerType[keyof typeof MaintenancePlanTriggerType];

export interface MaintenancePlanResponse {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  machineId: string;
  machineCode?: string | null;
  triggerType: MaintenancePlanTriggerType;
  intervalDays?: number | null;
  meterIntervalValue?: number | null;
  nextDueAt?: string | null;
  lastCompletedAt?: string | null;
  isActive: boolean;
  isOverdue: boolean;
  dueInDays?: number | null;
}

export interface BrowseMaintenancePlansRequest extends IPagedRequest {
  machineId?: string;
  isActive?: boolean;
  dueBefore?: string;
}

export interface GetDueMaintenancePlansRequest {
  overdueOnly?: boolean;
  dueWithinDays?: number;
}

export interface CreateMaintenancePlanRequest {
  code: string;
  name: string;
  description?: string | null;
  machineId: string;
  triggerType: MaintenancePlanTriggerType;
  intervalDays?: number | null;
  meterIntervalValue?: number | null;
  nextDueAt?: string | null;
  isActive: boolean;
}

export interface EvaluateDueMaintenancePlansRequest {
  currentMeterReading?: number | null;
}

const BASE = '/api/maintenance-plans';

export const maintenancePlanService = {
  async browse(req: BrowseMaintenancePlansRequest): Promise<IPagedResponse<MaintenancePlanResponse>> {
    const { data } = await http.get<IPagedResponse<MaintenancePlanResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async due(req: GetDueMaintenancePlansRequest = {}): Promise<MaintenancePlanResponse[]> {
    const { data } = await http.get<MaintenancePlanResponse[]>(`${BASE}/due`, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<MaintenancePlanResponse> {
    const { data } = await http.get<MaintenancePlanResponse>(`${BASE}/${encodeURIComponent(id)}`);
    return data;
  },
  async create(req: CreateMaintenancePlanRequest): Promise<MaintenancePlanResponse> {
    const { data } = await http.post<MaintenancePlanResponse>(BASE, req);
    return data;
  },
  async evaluateDue(req: EvaluateDueMaintenancePlansRequest = {}): Promise<MaintenanceWorkOrderResponse[]> {
    const { data } = await http.post<MaintenanceWorkOrderResponse[]>(`${BASE}/evaluate-due`, req);
    return data;
  },
  async raiseNow(id: string): Promise<MaintenanceWorkOrderResponse> {
    const { data } = await http.post<MaintenanceWorkOrderResponse>(`${BASE}/${encodeURIComponent(id)}/raise-now`, {});
    return data;
  }
};
