import { beforeEach, describe, expect, it, vi } from 'vitest';
import http from './http';
import {
  MaintenancePlanTriggerType,
  maintenancePlanService,
  type MaintenancePlanResponse
} from './maintenancePlanService';

vi.mock('./http', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn()
  }
}));

const getMock = vi.mocked(http.get);
const postMock = vi.mocked(http.post);

function plan(overrides: Partial<MaintenancePlanResponse> = {}): MaintenancePlanResponse {
  return {
    id: 'plan-1',
    code: 'PM-1',
    name: 'Monthly greasing',
    description: null,
    machineId: 'machine-1',
    machineCode: 'MC-1',
    triggerType: MaintenancePlanTriggerType.Time,
    intervalDays: 30,
    meterIntervalValue: null,
    nextDueAt: new Date('2026-09-20T00:00:00Z').toISOString(),
    lastCompletedAt: null,
    isActive: true,
    isOverdue: true,
    dueInDays: -4,
    ...overrides
  };
}

function page<T>(items: T[]): { totalCount: number; totalPages: number; items: T[] } {
  return { totalCount: items.length, totalPages: items.length > 0 ? 1 : 0, items };
}

describe('maintenancePlanService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('browse queries the plans endpoint with machine/active/dueBefore filters and paging', async () => {
    const response = page([plan()]);
    getMock.mockResolvedValue({ data: response });

    const result = await maintenancePlanService.browse({
      machineId: 'machine-1',
      isActive: true,
      dueBefore: '2026-09-26T00:00:00.000Z',
      pageNumber: 1,
      pageSize: 10
    });

    expect(result).toEqual(response);
    expect(getMock).toHaveBeenCalledOnce();
    expect(getMock).toHaveBeenCalledWith('/api/maintenance-plans', {
      params: {
        machineId: 'machine-1',
        isActive: true,
        dueBefore: '2026-09-26T00:00:00.000Z',
        pageNumber: 1,
        pageSize: 10
      }
    });
  });

  it('browse drops empty filters so the plans view lists every tenant plan', async () => {
    getMock.mockResolvedValue({ data: page([]) });

    await maintenancePlanService.browse({ machineId: undefined, isActive: undefined, dueBefore: undefined });

    const [, config] = getMock.mock.calls[0] as [string, { params: Record<string, unknown> }];
    expect(config.params).not.toHaveProperty('machineId');
    expect(config.params).not.toHaveProperty('isActive');
    expect(config.params).not.toHaveProperty('dueBefore');
  });

  it('due queries the due endpoint with the overdue and horizon filters', async () => {
    const response = [plan()];
    getMock.mockResolvedValue({ data: response });

    const result = await maintenancePlanService.due({ overdueOnly: true, dueWithinDays: 7 });

    expect(result).toEqual(response);
    expect(getMock).toHaveBeenCalledWith('/api/maintenance-plans/due', {
      params: { overdueOnly: true, dueWithinDays: 7 }
    });
  });

  it('due without filters lists every scheduled plan overdue first', async () => {
    getMock.mockResolvedValue({ data: [] });

    await maintenancePlanService.due({});

    expect(getMock).toHaveBeenCalledWith('/api/maintenance-plans/due', { params: {} });
  });

  it('get loads a single plan by id', async () => {
    const response = plan();
    getMock.mockResolvedValue({ data: response });

    const result = await maintenancePlanService.get('plan-1');

    expect(result).toEqual(response);
    expect(getMock).toHaveBeenCalledWith('/api/maintenance-plans/plan-1');
  });

  it('create posts the plan payload and returns the new plan', async () => {
    const created = plan();
    postMock.mockResolvedValue({ data: created });

    const result = await maintenancePlanService.create({
      code: 'PM-1',
      name: 'Monthly greasing',
      description: null,
      machineId: 'machine-1',
      triggerType: MaintenancePlanTriggerType.Time,
      intervalDays: 30,
      meterIntervalValue: null,
      nextDueAt: '2026-10-26T00:00:00.000Z',
      isActive: true
    });

    expect(result).toEqual(created);
    expect(postMock).toHaveBeenCalledWith('/api/maintenance-plans', {
      code: 'PM-1',
      name: 'Monthly greasing',
      description: null,
      machineId: 'machine-1',
      triggerType: MaintenancePlanTriggerType.Time,
      intervalDays: 30,
      meterIntervalValue: null,
      nextDueAt: '2026-10-26T00:00:00.000Z',
      isActive: true
    });
  });

  it('evaluateDue posts the meter reading and returns the raised orders', async () => {
    const raised = [{ id: 'order-1' }];
    postMock.mockResolvedValue({ data: raised });

    const result = await maintenancePlanService.evaluateDue({ currentMeterReading: 510 });

    expect(result).toEqual(raised);
    expect(postMock).toHaveBeenCalledWith('/api/maintenance-plans/evaluate-due', {
      currentMeterReading: 510
    });
  });

  it('raiseNow posts the manual raise for a single plan', async () => {
    const raised = { id: 'order-2' };
    postMock.mockResolvedValue({ data: raised });

    const result = await maintenancePlanService.raiseNow('plan-1');

    expect(result).toEqual(raised);
    expect(postMock).toHaveBeenCalledWith('/api/maintenance-plans/plan-1/raise-now', {});
  });

  it('propagates API errors (401 unauthenticated, 404 cross-tenant) to the caller', async () => {
    const unauthorized = new Error('Request failed with status code 401');
    getMock.mockRejectedValue(unauthorized);

    await expect(maintenancePlanService.due({})).rejects.toBe(unauthorized);

    const notFound = new Error('Request failed with status code 404');
    getMock.mockRejectedValue(notFound);

    await expect(maintenancePlanService.get('foreign-plan')).rejects.toBe(notFound);
  });
});
