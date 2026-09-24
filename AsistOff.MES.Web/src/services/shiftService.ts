import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

/**
 * A named working-time window. The API exchanges times as ISO 8601
 * (`HH:mm:ss`) but this service normalises them to `HH:mm` so views can bind
 * them straight to `<input type="time">`. A shift whose `endTime` is not after
 * its `startTime` crosses midnight (e.g. `22:00`–`06:00`).
 */
export interface ShiftResponse {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  startTime: string;
  endTime: string;
  isActive: boolean;
}

export interface BrowseShiftsRequest extends IPagedRequest {
  code?: string;
  name?: string;
  isActive?: boolean;
}

export interface CreateShiftRequest {
  code: string;
  name: string;
  description?: string | null;
  startTime: string;
  endTime: string;
  isActive: boolean;
}

export interface UpdateShiftRequest extends CreateShiftRequest {
  id: string;
}

const BASE = '/api/shifts';

/** `06:00:00` -> `06:00`. */
export function toDisplayTime(value: string): string {
  return value ? value.slice(0, 5) : value;
}

/** `06:00` -> `06:00:00` (System.Text.Json's `TimeOnly` converter). */
export function toApiTime(value: string): string {
  return value && value.length === 5 ? `${value}:00` : value;
}

function fromApi(shift: ShiftResponse): ShiftResponse {
  return { ...shift, startTime: toDisplayTime(shift.startTime), endTime: toDisplayTime(shift.endTime) };
}

function toApi<T extends { startTime: string; endTime: string }>(request: T): T {
  return { ...request, startTime: toApiTime(request.startTime), endTime: toApiTime(request.endTime) };
}

export const shiftService = {
  async browse(req: BrowseShiftsRequest): Promise<IPagedResponse<ShiftResponse>> {
    const { data } = await http.get<IPagedResponse<ShiftResponse>>(BASE, { params: buildPagedParams(req) });
    return { ...data, items: data.items.map(fromApi) };
  },
  async get(id: string): Promise<ShiftResponse> {
    const { data } = await http.get<ShiftResponse>(`${BASE}/${id}`);
    return fromApi(data);
  },
  async create(req: CreateShiftRequest): Promise<ShiftResponse> {
    const { data } = await http.post<ShiftResponse>(BASE, toApi(req));
    return fromApi(data);
  },
  async update(id: string, req: UpdateShiftRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, toApi(req));
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
