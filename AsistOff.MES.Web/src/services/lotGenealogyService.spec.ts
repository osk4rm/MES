import { beforeEach, describe, expect, it, vi } from 'vitest';
import http from './http';
import {
  LotGenealogyDepth,
  lotGenealogyService,
  normalizeGenealogyDepth,
  type LotTraceabilityResponse
} from './lotGenealogyService';

vi.mock('./http', () => ({
  default: {
    get: vi.fn()
  }
}));

const getMock = vi.mocked(http.get);

function trace(overrides: Partial<LotTraceabilityResponse> = {}): LotTraceabilityResponse {
  return {
    rootLotId: 'lot-root',
    rootLotCode: 'ROOT-1',
    nodes: [
      {
        lotId: 'lot-up',
        lotCode: 'UP-1',
        productId: 'product-1',
        depth: 1,
        consumedQuantity: 7,
        productionOrderId: 'order-1',
        productionOrderCode: 'ORDER-1',
        machineId: 'machine-1',
        reportedByOperatorId: null,
        occurredAt: new Date('2026-09-24T10:00:00Z').toISOString()
      }
    ],
    truncated: false,
    ...overrides
  };
}

describe('normalizeGenealogyDepth', () => {
  it('returns the default when no depth is given', () => {
    expect(normalizeGenealogyDepth(undefined)).toBe(LotGenealogyDepth.default);
    expect(normalizeGenealogyDepth(null)).toBe(LotGenealogyDepth.default);
    expect(normalizeGenealogyDepth(Number.NaN)).toBe(LotGenealogyDepth.default);
  });

  it('clamps out-of-range depths so invalid values are never sent', () => {
    expect(normalizeGenealogyDepth(0)).toBe(LotGenealogyDepth.min);
    expect(normalizeGenealogyDepth(-3)).toBe(LotGenealogyDepth.min);
    expect(normalizeGenealogyDepth(11)).toBe(LotGenealogyDepth.max);
    expect(normalizeGenealogyDepth(100)).toBe(LotGenealogyDepth.max);
  });

  it('floors fractional depths and keeps valid depths untouched', () => {
    expect(normalizeGenealogyDepth(2.7)).toBe(2);
    expect(normalizeGenealogyDepth(1)).toBe(1);
    expect(normalizeGenealogyDepth(10)).toBe(10);
  });
});

describe('lotGenealogyService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('getUpstream queries the upstream endpoint with the default depth', async () => {
    const expected = trace();
    getMock.mockResolvedValue({ data: expected });

    const result = await lotGenealogyService.getUpstream('lot-root');

    expect(result).toEqual(expected);
    expect(getMock).toHaveBeenCalledOnce();
    expect(getMock).toHaveBeenCalledWith('/api/lot-genealogy/upstream/lot-root', {
      params: { maxDepth: LotGenealogyDepth.default }
    });
  });

  it('getDownstream forwards the requested depth', async () => {
    const expected = trace({ nodes: [], truncated: true });
    getMock.mockResolvedValue({ data: expected });

    const result = await lotGenealogyService.getDownstream('lot-root', 3);

    expect(result).toEqual(expected);
    expect(getMock).toHaveBeenCalledWith('/api/lot-genealogy/downstream/lot-root', {
      params: { maxDepth: 3 }
    });
  });

  it('never sends an invalid depth to the API', async () => {
    getMock.mockResolvedValue({ data: trace({ nodes: [] }) });

    await lotGenealogyService.getUpstream('lot-root', 0);
    await lotGenealogyService.getDownstream('lot-root', 11);

    expect(getMock).toHaveBeenCalledTimes(2);
    const [, upstreamConfig] = getMock.mock.calls[0] as [string, { params: { maxDepth: number } }];
    const [, downstreamConfig] = getMock.mock.calls[1] as [string, { params: { maxDepth: number } }];
    expect(upstreamConfig.params.maxDepth).toBe(LotGenealogyDepth.min);
    expect(downstreamConfig.params.maxDepth).toBe(LotGenealogyDepth.max);
  });

  it('propagates API errors (e.g. 404 for cross-tenant lots) to the caller', async () => {
    const failure = new Error('Request failed with status code 404');
    getMock.mockRejectedValue(failure);

    await expect(lotGenealogyService.getUpstream('foreign-lot')).rejects.toBe(failure);
    await expect(lotGenealogyService.getDownstream('foreign-lot')).rejects.toBe(failure);
  });
});
