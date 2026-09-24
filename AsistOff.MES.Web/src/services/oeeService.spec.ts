import { beforeEach, describe, expect, it, vi } from 'vitest';
import http from './http';
import {
  formatOeeFactor,
  formatOeeShare,
  hasNullFactors,
  oeeService,
  paretoLabel,
  type OeeLosses,
  type OeeSnapshot,
  type OeeTrend
} from './oeeService';

vi.mock('./http', () => ({
  default: {
    get: vi.fn()
  }
}));

const getMock = vi.mocked(http.get);

function snapshot(overrides: Partial<OeeSnapshot> = {}): OeeSnapshot {
  return {
    machineId: 'machine-1',
    fromUtc: new Date('2026-09-24T06:00:00Z').toISOString(),
    toUtc: new Date('2026-09-24T14:00:00Z').toISOString(),
    idealCycleTimeSeconds: 60,
    availability: 0.875,
    performance: 0.9,
    quality: 0.95,
    oee: 0.7481,
    availabilityComputed: true,
    performanceComputed: true,
    qualityComputed: true,
    plannedProductionTimeMinutes: 480,
    runTimeMinutes: 420,
    downtimeMinutes: 60,
    totalCount: 100,
    goodCount: 95,
    scrapCount: 5,
    ...overrides
  };
}

function trend(overrides: Partial<OeeTrend> = {}): OeeTrend {
  return {
    machineId: 'machine-1',
    fromUtc: new Date('2026-09-24T06:00:00Z').toISOString(),
    toUtc: new Date('2026-09-24T14:00:00Z').toISOString(),
    idealCycleTimeSeconds: 60,
    bucket: 'Day',
    buckets: [snapshot()],
    ...overrides
  };
}

function losses(overrides: Partial<OeeLosses> = {}): OeeLosses {
  return {
    machineId: 'machine-1',
    fromUtc: new Date('2026-09-24T06:00:00Z').toISOString(),
    toUtc: new Date('2026-09-24T14:00:00Z').toISOString(),
    totalDowntimeMinutes: 90,
    downtimePareto: [
      { reasonCodeId: 'reason-1', code: 'DT-1', displayName: 'Breakdown', minutes: 60, share: 60 / 90 }
    ],
    totalScrapQuantity: 15,
    scrapPareto: [
      { reasonCodeId: 'reason-2', code: 'SCR-1', displayName: 'Tolerance over', quantity: 10, share: 10 / 15 }
    ],
    ...overrides
  };
}

describe('oeeService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('getSnapshot queries the snapshot endpoint with the work center and window', async () => {
    const expected = snapshot();
    getMock.mockResolvedValue({ data: expected });

    const result = await oeeService.getSnapshot({
      machineId: 'machine-1',
      fromUtc: expected.fromUtc,
      toUtc: expected.toUtc,
      idealCycleTimeSeconds: 60
    });

    expect(result).toEqual(expected);
    expect(getMock).toHaveBeenCalledOnce();
    expect(getMock).toHaveBeenCalledWith('/api/oee/snapshot', {
      params: {
        machineId: 'machine-1',
        fromUtc: expected.fromUtc,
        toUtc: expected.toUtc,
        idealCycleTimeSeconds: 60
      }
    });
  });

  it('getTrend forwards the bucket granularity', async () => {
    const expected = trend({ bucket: 'Week' });
    getMock.mockResolvedValue({ data: expected });

    const result = await oeeService.getTrend({
      machineId: 'machine-1',
      fromUtc: expected.fromUtc,
      toUtc: expected.toUtc,
      idealCycleTimeSeconds: 60,
      bucket: 'Week'
    });

    expect(result).toEqual(expected);
    expect(getMock).toHaveBeenCalledWith('/api/oee/trend', {
      params: {
        machineId: 'machine-1',
        fromUtc: expected.fromUtc,
        toUtc: expected.toUtc,
        idealCycleTimeSeconds: 60,
        bucket: 'Week'
      }
    });
  });

  it('getLosses queries the losses endpoint without an ideal cycle time', async () => {
    const expected = losses();
    getMock.mockResolvedValue({ data: expected });

    const result = await oeeService.getLosses({
      machineId: 'machine-1',
      fromUtc: expected.fromUtc,
      toUtc: expected.toUtc
    });

    expect(result).toEqual(expected);
    expect(getMock).toHaveBeenCalledWith('/api/oee/losses', {
      params: {
        machineId: 'machine-1',
        fromUtc: expected.fromUtc,
        toUtc: expected.toUtc
      }
    });
  });

  it('propagates API errors (e.g. 404 for cross-tenant work centers) to the caller', async () => {
    const failure = new Error('Request failed with status code 404');
    getMock.mockRejectedValue(failure);

    await expect(
      oeeService.getSnapshot({ machineId: 'foreign', fromUtc: 'a', toUtc: 'b', idealCycleTimeSeconds: 60 })
    ).rejects.toBe(failure);
    await expect(
      oeeService.getTrend({ machineId: 'foreign', fromUtc: 'a', toUtc: 'b', idealCycleTimeSeconds: 60, bucket: 'Day' })
    ).rejects.toBe(failure);
    await expect(
      oeeService.getLosses({ machineId: 'foreign', fromUtc: 'a', toUtc: 'b' })
    ).rejects.toBe(failure);
  });
});

describe('formatOeeFactor', () => {
  it('formats ratios as percentages with one decimal', () => {
    expect(formatOeeFactor(0.875)).toBe('87.5%');
    expect(formatOeeFactor(1)).toBe('100.0%');
    expect(formatOeeFactor(0)).toBe('0.0%');
  });

  it('renders null factors as an em dash, never as zeros', () => {
    expect(formatOeeFactor(null)).toBe('—');
    expect(formatOeeFactor(undefined)).toBe('—');
    expect(formatOeeFactor(Number.NaN)).toBe('—');
  });
});

describe('formatOeeShare', () => {
  it('formats shares as percentages with one decimal', () => {
    expect(formatOeeShare(60 / 90)).toBe('66.7%');
  });

  it('renders missing shares as an em dash', () => {
    expect(formatOeeShare(null)).toBe('—');
    expect(formatOeeShare(undefined)).toBe('—');
  });
});

describe('hasNullFactors', () => {
  it('is true when no factor could be computed (e.g. no planned time)', () => {
    expect(
      hasNullFactors(
        snapshot({
          availability: null,
          performance: null,
          quality: null,
          oee: null,
          availabilityComputed: false,
          performanceComputed: false,
          qualityComputed: false
        })
      )
    ).toBe(true);
  });

  it('is false when any factor was computed', () => {
    expect(hasNullFactors(snapshot())).toBe(false);
    expect(hasNullFactors(snapshot({ availabilityComputed: true, performanceComputed: false, qualityComputed: false }))).toBe(
      false
    );
  });

  it('is false without a snapshot', () => {
    expect(hasNullFactors(null)).toBe(false);
  });
});

describe('paretoLabel', () => {
  it('prefers the display name over the code', () => {
    expect(paretoLabel({ displayName: 'Breakdown', code: 'DT-1' })).toBe('Breakdown');
  });

  it('falls back to the code and finally to an em dash', () => {
    expect(paretoLabel({ displayName: null, code: 'DT-1' })).toBe('DT-1');
    expect(paretoLabel({ displayName: null, code: null })).toBe('—');
  });
});
