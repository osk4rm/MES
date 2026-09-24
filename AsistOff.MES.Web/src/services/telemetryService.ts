import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export const TelemetryDataType = {
  Boolean: 1,
  Double: 2,
  Integer: 3,
  String: 4
} as const;
export type TelemetryDataType = typeof TelemetryDataType[keyof typeof TelemetryDataType];

export const TelemetryQuality = {
  Good: 1,
  Bad: 2,
  Uncertain: 3
} as const;
export type TelemetryQuality = typeof TelemetryQuality[keyof typeof TelemetryQuality];

export interface MachineTelemetryTagResponse {
  id: string;
  machineId: string;
  nodeId: string;
  displayName: string;
  dataType: TelemetryDataType;
  pollIntervalSeconds: number;
  isEnabled: boolean;
  description?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface BrowseMachineTelemetryTagsRequest extends IPagedRequest {
  machineId?: string;
  isEnabled?: boolean;
  search?: string;
}

export interface CreateMachineTelemetryTagRequest {
  machineId: string;
  nodeId: string;
  displayName: string;
  dataType: TelemetryDataType;
  pollIntervalSeconds: number;
  description?: string | null;
}

export interface UpdateMachineTelemetryTagRequest {
  id: string;
  displayName: string;
  dataType: TelemetryDataType;
  pollIntervalSeconds: number;
  isEnabled: boolean;
  description?: string | null;
}

export interface TelemetryReadingResponse {
  id: string;
  tagId: string;
  machineId: string;
  readAt: string;
  doubleValue?: number | null;
  stringValue?: string | null;
  quality: TelemetryQuality;
}

export interface BrowseTelemetryReadingsRequest extends IPagedRequest {
  tagId?: string;
  machineId?: string;
  readAtFrom?: string;
  readAtTo?: string;
  latestOnly?: boolean;
}

export interface SubmitTelemetryReadingRequest {
  tagId: string;
  readAt: string;
  doubleValue?: number | null;
  stringValue?: string | null;
  quality: TelemetryQuality;
}

export interface TelemetryTagStatusEntry {
  tagId: string;
  machineId: string;
  nodeId: string;
  displayName: string;
  isEnabled: boolean;
  lastReadAt?: string | null;
  readingsLastHour: number;
  stale: boolean;
}

export interface TelemetryStatusResponse {
  simulatorEnabled: boolean;
  simulatorIntervalSeconds: number;
  tags: TelemetryTagStatusEntry[];
}

const TAGS_BASE = '/api/telemetry-tags';
const READINGS_BASE = '/api/telemetry-readings';

export const telemetryTagService = {
  async browse(req: BrowseMachineTelemetryTagsRequest): Promise<IPagedResponse<MachineTelemetryTagResponse>> {
    const { data } = await http.get<IPagedResponse<MachineTelemetryTagResponse>>(TAGS_BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<MachineTelemetryTagResponse> {
    const { data } = await http.get<MachineTelemetryTagResponse>(`${TAGS_BASE}/${id}`);
    return data;
  },
  async create(req: CreateMachineTelemetryTagRequest): Promise<MachineTelemetryTagResponse> {
    const { data } = await http.post<MachineTelemetryTagResponse>(TAGS_BASE, req);
    return data;
  },
  async update(id: string, req: UpdateMachineTelemetryTagRequest): Promise<void> {
    await http.put(`${TAGS_BASE}/${id}`, req);
  },
  async toggle(id: string): Promise<MachineTelemetryTagResponse> {
    const { data } = await http.post<MachineTelemetryTagResponse>(`${TAGS_BASE}/${id}/toggle`);
    return data;
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${TAGS_BASE}/${id}`);
  },
  async getStatus(): Promise<TelemetryStatusResponse> {
    const { data } = await http.get<TelemetryStatusResponse>(`${TAGS_BASE}/status`);
    return data;
  }
};

export const telemetryReadingService = {
  async browse(req: BrowseTelemetryReadingsRequest): Promise<IPagedResponse<TelemetryReadingResponse>> {
    const { data } = await http.get<IPagedResponse<TelemetryReadingResponse>>(READINGS_BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<TelemetryReadingResponse> {
    const { data } = await http.get<TelemetryReadingResponse>(`${READINGS_BASE}/${id}`);
    return data;
  },
  async submit(req: SubmitTelemetryReadingRequest): Promise<TelemetryReadingResponse> {
    const { data } = await http.post<TelemetryReadingResponse>(READINGS_BASE, req);
    return data;
  },
  async trend(tagId: string, take = 50): Promise<TelemetryReadingResponse[]> {
    const { data } = await http.get<TelemetryReadingResponse[]>(`${READINGS_BASE}/trend`, { params: { tagId, take } });
    return data;
  },
  async downloadCsv(req: BrowseTelemetryReadingsRequest): Promise<Blob> {
    const { data } = await http.get<Blob>(READINGS_BASE, {
      params: buildPagedParams(req),
      headers: { Accept: 'text/csv' },
      responseType: 'blob'
    });
    return data;
  }
};
