import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

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
  name?: string;
  code?: string;
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

/** Day of week matching the backend DayOfWeek enum (Sunday = 0). */
export const weekDays = [
  { value: 1, key: 'monday' },
  { value: 2, key: 'tuesday' },
  { value: 3, key: 'wednesday' },
  { value: 4, key: 'thursday' },
  { value: 5, key: 'friday' },
  { value: 6, key: 'saturday' },
  { value: 0, key: 'sunday' }
] as const;

export interface WorkCenterCalendarEntry {
  id?: string;
  dayOfWeek: number;
  startTime: string;
  endTime: string;
  shiftId?: string | null;
  shiftCode?: string | null;
  shiftName?: string | null;
  isWorking: boolean;
}

export interface WorkCenterCalendar {
  id: string;
  machineId: string;
  entries: WorkCenterCalendarEntry[];
}

const BASE = '/api/shifts';

function normalizeTime(value: string): string {
  return value.length === 5 ? `${value}:00` : value;
}

export const shiftService = {
  async browse(req: BrowseShiftsRequest): Promise<IPagedResponse<ShiftResponse>> {
    const { data } = await http.get<IPagedResponse<ShiftResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async list(req: BrowseShiftsRequest): Promise<ShiftResponse[]> {
    const page = await shiftService.browse({ ...req, pageNumber: 1, pageSize: 200 });
    return page.items;
  },
  async get(id: string): Promise<ShiftResponse> {
    const { data } = await http.get<ShiftResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateShiftRequest): Promise<ShiftResponse> {
    const { data } = await http.post<ShiftResponse>(BASE, {
      ...req,
      startTime: normalizeTime(req.startTime),
      endTime: normalizeTime(req.endTime)
    });
    return data;
  },
  async update(id: string, req: UpdateShiftRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, {
      ...req,
      startTime: normalizeTime(req.startTime),
      endTime: normalizeTime(req.endTime)
    });
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
