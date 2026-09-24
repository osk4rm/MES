import { beforeEach, describe, expect, it, vi } from 'vitest';
import http from './http';
import {
  MaintenanceWorkOrderPriority,
  MaintenanceWorkOrderStatus,
  maintenanceWorkOrderService,
  type MaintenanceWorkOrderResponse
} from './maintenanceWorkOrderService';

vi.mock('./http', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn()
  }
}));

const getMock = vi.mocked(http.get);
const postMock = vi.mocked(http.post);

function order(overrides: Partial<MaintenanceWorkOrderResponse> = {}): MaintenanceWorkOrderResponse {
  return {
    id: 'order-1',
    code: 'WO-1',
    title: 'Fix spindle',
    description: 'Spindle vibrates at high RPM',
    machineId: 'machine-1',
    machineCode: 'MC-1',
    priority: MaintenanceWorkOrderPriority.High,
    status: MaintenanceWorkOrderStatus.Open,
    reportedAt: new Date('2026-09-24T10:00:00Z').toISOString(),
    startedAt: null,
    completedAt: null,
    resolutionNotes: null,
    ...overrides
  };
}

function page<T>(items: T[]): { totalCount: number; totalPages: number; items: T[] } {
  return { totalCount: items.length, totalPages: items.length > 0 ? 1 : 0, items };
}

describe('maintenanceWorkOrderService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('browse queries the board endpoint with machine/status filters and paging', async () => {
    const response = page([order()]);
    getMock.mockResolvedValue({ data: response });

    const result = await maintenanceWorkOrderService.browse({
      machineId: 'machine-1',
      status: MaintenanceWorkOrderStatus.InProgress,
      pageNumber: 1,
      pageSize: 10
    });

    expect(result).toEqual(response);
    expect(getMock).toHaveBeenCalledOnce();
    expect(getMock).toHaveBeenCalledWith('/api/maintenance-work-orders', {
      params: {
        machineId: 'machine-1',
        status: MaintenanceWorkOrderStatus.InProgress,
        pageNumber: 1,
        pageSize: 10
      }
    });
  });

  it('browse drops empty filters so the board lists every tenant order', async () => {
    getMock.mockResolvedValue({ data: page([]) });

    await maintenanceWorkOrderService.browse({ machineId: undefined, status: undefined });

    const [, config] = getMock.mock.calls[0] as [string, { params: Record<string, unknown> }];
    expect(config.params).not.toHaveProperty('machineId');
    expect(config.params).not.toHaveProperty('status');
  });

  it('get loads a single work order by id', async () => {
    const response = order();
    getMock.mockResolvedValue({ data: response });

    const result = await maintenanceWorkOrderService.get('order-1');

    expect(result).toEqual(response);
    expect(getMock).toHaveBeenCalledWith('/api/maintenance-work-orders/order-1');
  });

  it('create posts code, title, machine and priority and returns the new Open order', async () => {
    const created = order();
    postMock.mockResolvedValue({ data: created });

    const result = await maintenanceWorkOrderService.create({
      code: 'WO-1',
      title: 'Fix spindle',
      description: null,
      machineId: 'machine-1',
      priority: MaintenanceWorkOrderPriority.High
    });

    expect(result).toEqual(created);
    expect(postMock).toHaveBeenCalledWith('/api/maintenance-work-orders', {
      code: 'WO-1',
      title: 'Fix spindle',
      description: null,
      machineId: 'machine-1',
      priority: MaintenanceWorkOrderPriority.High
    });
  });

  it('start posts the Open -> InProgress transition', async () => {
    const started = order({ status: MaintenanceWorkOrderStatus.InProgress });
    postMock.mockResolvedValue({ data: started });

    const result = await maintenanceWorkOrderService.start('order-1');

    expect(result).toEqual(started);
    expect(postMock).toHaveBeenCalledWith('/api/maintenance-work-orders/order-1/start', {});
  });

  it('complete posts the resolution notes for the Done transition', async () => {
    const done = order({ status: MaintenanceWorkOrderStatus.Done, resolutionNotes: 'Replaced bearing' });
    postMock.mockResolvedValue({ data: done });

    const result = await maintenanceWorkOrderService.complete('order-1', { resolutionNotes: 'Replaced bearing' });

    expect(result).toEqual(done);
    expect(postMock).toHaveBeenCalledWith('/api/maintenance-work-orders/order-1/complete', {
      resolutionNotes: 'Replaced bearing'
    });
  });

  it('cancel posts the Cancelled transition', async () => {
    const cancelled = order({ status: MaintenanceWorkOrderStatus.Cancelled });
    postMock.mockResolvedValue({ data: cancelled });

    const result = await maintenanceWorkOrderService.cancel('order-1');

    expect(result).toEqual(cancelled);
    expect(postMock).toHaveBeenCalledWith('/api/maintenance-work-orders/order-1/cancel', {});
  });

  it('propagates API errors (400 invalid transition, 404 cross-tenant) to the caller', async () => {
    const invalidTransition = new Error('Request failed with status code 400');
    postMock.mockRejectedValue(invalidTransition);

    await expect(maintenanceWorkOrderService.start('order-1')).rejects.toBe(invalidTransition);

    const notFound = new Error('Request failed with status code 404');
    getMock.mockRejectedValue(notFound);

    await expect(maintenanceWorkOrderService.get('foreign-order')).rejects.toBe(notFound);
  });
});
