import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export const MaintenanceWorkOrderStatus = {
  Open: 1,
  InProgress: 2,
  Done: 3,
  Cancelled: 4
} as const;
export type MaintenanceWorkOrderStatus = typeof MaintenanceWorkOrderStatus[keyof typeof MaintenanceWorkOrderStatus];

export const MaintenanceWorkOrderPriority = {
  Low: 1,
  Medium: 2,
  High: 3,
  Critical: 4
} as const;
export type MaintenanceWorkOrderPriority = typeof MaintenanceWorkOrderPriority[keyof typeof MaintenanceWorkOrderPriority];

export interface MaintenanceWorkOrderResponse {
  id: string;
  code: string;
  title: string;
  description?: string | null;
  machineId: string;
  machineCode?: string | null;
  planId?: string | null;
  priority: MaintenanceWorkOrderPriority;
  status: MaintenanceWorkOrderStatus;
  reportedAt: string;
  startedAt?: string | null;
  completedAt?: string | null;
  resolutionNotes?: string | null;
}

export interface BrowseMaintenanceWorkOrdersRequest extends IPagedRequest {
  machineId?: string;
  status?: MaintenanceWorkOrderStatus;
}

export interface CreateMaintenanceWorkOrderRequest {
  code: string;
  title: string;
  description?: string | null;
  machineId: string;
  priority: MaintenanceWorkOrderPriority;
}

export interface CompleteMaintenanceWorkOrderRequest {
  resolutionNotes?: string | null;
}

const BASE = '/api/maintenance-work-orders';

export const maintenanceWorkOrderService = {
  async browse(req: BrowseMaintenanceWorkOrdersRequest): Promise<IPagedResponse<MaintenanceWorkOrderResponse>> {
    const { data } = await http.get<IPagedResponse<MaintenanceWorkOrderResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<MaintenanceWorkOrderResponse> {
    const { data } = await http.get<MaintenanceWorkOrderResponse>(`${BASE}/${encodeURIComponent(id)}`);
    return data;
  },
  async create(req: CreateMaintenanceWorkOrderRequest): Promise<MaintenanceWorkOrderResponse> {
    const { data } = await http.post<MaintenanceWorkOrderResponse>(BASE, req);
    return data;
  },
  async start(id: string): Promise<MaintenanceWorkOrderResponse> {
    const { data } = await http.post<MaintenanceWorkOrderResponse>(`${BASE}/${encodeURIComponent(id)}/start`, {});
    return data;
  },
  async complete(id: string, req: CompleteMaintenanceWorkOrderRequest): Promise<MaintenanceWorkOrderResponse> {
    const { data } = await http.post<MaintenanceWorkOrderResponse>(`${BASE}/${encodeURIComponent(id)}/complete`, req);
    return data;
  },
  async cancel(id: string): Promise<MaintenanceWorkOrderResponse> {
    const { data } = await http.post<MaintenanceWorkOrderResponse>(`${BASE}/${encodeURIComponent(id)}/cancel`, {});
    return data;
  }
};
