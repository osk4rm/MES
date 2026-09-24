import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export interface MachineResponse {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  departmentId?: string | null;
  isActive: boolean;
}

export interface BrowseMachinesRequest extends IPagedRequest {
  name?: string;
  code?: string;
  isActive?: boolean;
  departmentId?: string;
}

export interface CreateMachineRequest {
  code: string;
  name: string;
  description?: string | null;
  departmentId?: string | null;
  isActive: boolean;
}

export interface UpdateMachineRequest extends CreateMachineRequest {
  id: string;
}

/**
 * .NET's `System.DayOfWeek` numbering used by the calendar API:
 * Sunday = 0 … Saturday = 6.
 */
export const DayOfWeek = {
  Sunday: 0,
  Monday: 1,
  Tuesday: 2,
  Wednesday: 3,
  Thursday: 4,
  Friday: 5,
  Saturday: 6
} as const;
export type DayOfWeek = typeof DayOfWeek[keyof typeof DayOfWeek];

/** One recurring weekly window of a Work Center calendar. Times are `HH:mm`. */
export interface WorkCenterCalendarEntryResponse {
  id: string;
  dayOfWeek: DayOfWeek;
  startTime: string;
  endTime: string;
  shiftId?: string | null;
  isWorking: boolean;
}

export interface WorkCenterCalendarResponse {
  id: string;
  machineId: string;
  machineCode: string;
  machineName: string;
  entries: WorkCenterCalendarEntryResponse[];
}

export interface SaveWorkCenterCalendarEntryRequest {
  dayOfWeek: DayOfWeek;
  startTime: string;
  endTime: string;
  shiftId?: string | null;
  isWorking: boolean;
}

export interface SaveWorkCenterCalendarRequest {
  entries: SaveWorkCenterCalendarEntryRequest[];
}

const BASE = '/api/machines';

/** `06:00:00` -> `06:00`. */
function toDisplayTime(value: string): string {
  return value ? value.slice(0, 5) : value;
}

/** `06:00` -> `06:00:00` (System.Text.Json's `TimeOnly` converter). */
function toApiTime(value: string): string {
  return value && value.length === 5 ? `${value}:00` : value;
}

function normalizeCalendar(calendar: WorkCenterCalendarResponse): WorkCenterCalendarResponse {
  return {
    ...calendar,
    entries: calendar.entries.map(entry => ({
      ...entry,
      startTime: toDisplayTime(entry.startTime),
      endTime: toDisplayTime(entry.endTime)
    }))
  };
}

export const machineService = {
  async browse(req: BrowseMachinesRequest): Promise<IPagedResponse<MachineResponse>> {
    const { data } = await http.get<IPagedResponse<MachineResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<MachineResponse> {
    const { data } = await http.get<MachineResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateMachineRequest): Promise<MachineResponse> {
    const { data } = await http.post<MachineResponse>(BASE, req);
    return data;
  },
  async update(id: string, req: UpdateMachineRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, req);
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  },
  async getCalendar(machineId: string): Promise<WorkCenterCalendarResponse> {
    const { data } = await http.get<WorkCenterCalendarResponse>(`${BASE}/${machineId}/calendar`);
    return normalizeCalendar(data);
  },
  async saveCalendar(
    machineId: string,
    req: SaveWorkCenterCalendarRequest
  ): Promise<WorkCenterCalendarResponse> {
    const { data } = await http.put<WorkCenterCalendarResponse>(`${BASE}/${machineId}/calendar`, {
      entries: req.entries.map(entry => ({
        ...entry,
        startTime: toApiTime(entry.startTime),
        endTime: toApiTime(entry.endTime)
      }))
    });
    return normalizeCalendar(data);
  }
};
