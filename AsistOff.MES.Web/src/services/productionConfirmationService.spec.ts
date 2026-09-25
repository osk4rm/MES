import { beforeEach, describe, expect, it, vi } from 'vitest';
import http from './http';
import {
  productionConfirmationService,
  type ProductionConfirmationResponse
} from './productionConfirmationService';

vi.mock('./http', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    delete: vi.fn()
  }
}));

const getMock = vi.mocked(http.get);
const postMock = vi.mocked(http.post);
const deleteMock = vi.mocked(http.delete);

function confirmation(overrides: Partial<ProductionConfirmationResponse> = {}): ProductionConfirmationResponse {
  return {
    id: 'conf-1',
    productionOrderId: 'order-1',
    machineId: 'machine-1',
    reportedByOperatorId: null,
    reportedAt: new Date('2026-09-24T10:00:00Z').toISOString(),
    goodQuantity: 10,
    scrapQuantity: 2,
    notes: null,
    createdAt: new Date('2026-09-24T10:00:00Z').toISOString(),
    updatedAt: null,
    ...overrides
  };
}

describe('productionConfirmationService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('browse forwards per-order filters and paging without a page reload contract', async () => {
    const page = { totalCount: 1, totalPages: 1, items: [confirmation()] };
    getMock.mockResolvedValue({ data: page });

    const result = await productionConfirmationService.browse({
      productionOrderId: 'order-1',
      machineId: 'machine-1',
      from: '2026-09-24T00:00:00.000Z',
      to: '2026-09-25T00:00:00.000Z',
      pageNumber: 1,
      pageSize: 50
    });

    expect(result).toEqual(page);
    expect(getMock).toHaveBeenCalledOnce();
    const [url, config] = getMock.mock.calls[0] as [string, { params: Record<string, unknown> }];
    expect(url).toBe('/api/production-confirmations');
    expect(config.params).toMatchObject({
      productionOrderId: 'order-1',
      machineId: 'machine-1',
      pageNumber: 1,
      pageSize: 50
    });
  });

  it('browse drops empty filters so the list stays per-order', async () => {
    getMock.mockResolvedValue({ data: { totalCount: 0, totalPages: 0, items: [] } });

    await productionConfirmationService.browse({ productionOrderId: 'order-1' });

    const [, config] = getMock.mock.calls[0] as [string, { params: Record<string, unknown> }];
    expect(config.params).toMatchObject({ productionOrderId: 'order-1' });
    expect(config.params).not.toHaveProperty('machineId');
    expect(config.params).not.toHaveProperty('from');
    expect(config.params).not.toHaveProperty('to');
  });

  it('create posts the report payload and returns the created record', async () => {
    const created = confirmation({ goodQuantity: 7, scrapQuantity: 1 });
    postMock.mockResolvedValue({ data: created });
    const payload = {
      productionOrderId: 'order-1',
      machineId: 'machine-1',
      reportedByOperatorId: null,
      reportedAt: new Date('2026-09-24T10:00:00Z').toISOString(),
      goodQuantity: 7,
      scrapQuantity: 1,
      notes: null
    };

    const result = await productionConfirmationService.create(payload);

    expect(result).toEqual(created);
    expect(postMock).toHaveBeenCalledWith('/api/production-confirmations', payload);
  });

  it('create forwards produced lot and consumed lots for genealogy trace (#221)', async () => {
    const created = confirmation({ goodQuantity: 7, scrapQuantity: 1 });
    postMock.mockResolvedValue({ data: created });
    const payload = {
      productionOrderId: 'order-1',
      machineId: 'machine-1',
      reportedByOperatorId: null,
      reportedAt: new Date('2026-09-24T10:00:00Z').toISOString(),
      goodQuantity: 7,
      scrapQuantity: 1,
      notes: null,
      producedLotId: 'lot-produced',
      consumedLots: [
        { lotId: 'lot-a', quantity: 5 },
        { lotId: 'lot-b', quantity: 7 }
      ]
    };

    const result = await productionConfirmationService.create(payload);

    expect(result).toEqual(created);
    expect(postMock).toHaveBeenCalledWith('/api/production-confirmations', payload);
  });

  it('create posts null produced lot and empty consumed lots when no trace is recorded', async () => {
    const created = confirmation();
    postMock.mockResolvedValue({ data: created });
    const payload = {
      productionOrderId: 'order-1',
      machineId: 'machine-1',
      reportedByOperatorId: null,
      reportedAt: new Date('2026-09-24T10:00:00Z').toISOString(),
      goodQuantity: 10,
      scrapQuantity: 2,
      notes: null,
      producedLotId: null,
      consumedLots: []
    };

    await productionConfirmationService.create(payload);

    expect(postMock).toHaveBeenCalledWith('/api/production-confirmations', payload);
  });

  it('get fetches a single confirmation by id', async () => {
    const record = confirmation();
    getMock.mockResolvedValue({ data: record });

    const result = await productionConfirmationService.get('conf-1');

    expect(result).toEqual(record);
    expect(getMock).toHaveBeenCalledWith('/api/production-confirmations/conf-1');
  });

  it('remove issues a delete for the confirmation id', async () => {
    deleteMock.mockResolvedValue({ data: undefined });

    await productionConfirmationService.remove('conf-1');

    expect(deleteMock).toHaveBeenCalledWith('/api/production-confirmations/conf-1');
  });
});
