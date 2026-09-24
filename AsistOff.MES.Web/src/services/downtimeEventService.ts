import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export const DowntimeEventStatus = {
  Open: 1,
  Closed: 2
} as const;
export type DowntimeEventStatus = typeof DowntimeEventStatus[keyof typeof DowntimeEventStatus];

export interface DowntimeEventResponse {
  id: string;
  machineId: string;
  reasonCodeId: string;
  startedAt: string;
  endedAt?: string | null;
  status: DowntimeEventStatus;
  durationMinutes?: number | null;
  notes?: string | null;
  reportedByOperatorId?: string | null;
  productionOrderId?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface BrowseDowntimeEventsRequest extends IPagedRequest {
  machineId?: string;
  reasonCodeId?: string;
  status?: DowntimeEventStatus;
  startedFrom?: string;
  startedTo?: string;
}

export interface StartDowntimeEventRequest {
  machineId: string;
  reasonCodeId: string;
  startedAt: string;
  notes?: string | null;
  reportedByOperatorId?: string | null;
}

export interface UpdateDowntimeEventRequest {
  id: string;
  reasonCodeId: string;
  notes?: string | null;
}

export interface CloseDowntimeEventRequest {
  endedAt?: string | null;
}

const BASE = '/api/downtime-events';

export const downtimeEventService = {
  async browse(req: BrowseDowntimeEventsRequest): Promise<IPagedResponse<DowntimeEventResponse>> {
    const { data } = await http.get<IPagedResponse<DowntimeEventResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<DowntimeEventResponse> {
    const { data } = await http.get<DowntimeEventResponse>(`${BASE}/${id}`);
    return data;
  },
  async start(req: StartDowntimeEventRequest): Promise<DowntimeEventResponse> {
    const { data } = await http.post<DowntimeEventResponse>(BASE, req);
    return data;
  },
  async close(id: string, req: CloseDowntimeEventRequest): Promise<DowntimeEventResponse> {
    const { data } = await http.post<DowntimeEventResponse>(`${BASE}/${id}/close`, req);
    return data;
  },
  async update(id: string, req: UpdateDowntimeEventRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, req);
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
