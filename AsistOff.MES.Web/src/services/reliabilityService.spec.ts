import { beforeEach, describe, expect, it, vi } from 'vitest';
import http from './http';
import {
  ReliabilityTrendBucket,
  formatMinutes,
  formatNullableMinutes,
  hasNullReliability,
  reliabilityService,
  validateReliabilityBucket,
  validateReliabilityWindow,
  type ReliabilityFleetRow,
  type ReliabilitySnapshot,
  type ReliabilityTrend
} from './reliabilityService';

vi.mock('./http', () => ({
  default: {
    get: vi.fn()
  }
}));

const getMock = vi.mocked(http.get);

function snapshot(overrides: Partial<ReliabilitySnapshot> = {}): ReliabilitySnapshot {
  const fromUtc = new Date('2026-09-24T06:00:00Z').toISOString();
  const toUtc = new Date('2026-09-24T14:00:00Z').toISOString();
  return {
    machineId: 'machine-1',
    fromUtc,
    toUtc,
    failureCount: 1,
    repairCount: 1,
    windowMinutes: 480,
    uptimeMinutes: 420,
    totalDowntimeMinutes: 60,
    mtbfMinutes: 420,
    mttrMinutes: 60,
    avgRepairMinutes: 45,
    ...overrides
  };
}

function trend(overrides: Partial<ReliabilityTrend> = {}): ReliabilityTrend {
  return {
    machineId: 'machine-1',
    fromUtc: new Date('2026-09-24T06:00:00Z').toISOString(),
    toUtc: new Date('2026-10-08T06:00:00Z').toISOString(),
    bucket: ReliabilityTrendBucket.Week,
    buckets: [snapshot()],
    ...overrides
  };
}

function fleetRow(overrides: Partial<ReliabilityFleetRow> = {}): ReliabilityFleetRow {
  return {
    machineId: 'machine-1',
    machineCode: 'WC-1',
    machineName: 'Work Center 1',
    departmentId: null,
    failureCount: 1,
    repairCount: 1,
    windowMinutes: 480,
    uptimeMinutes: 420,
    totalDowntimeMinutes: 60,
    mtbfMinutes: 420,
    mttrMinutes: 60,
    avgRepairMinutes: 45,
    ...overrides
  };
}

describe('reliabilityService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('getSnapshot queries the snapshot endpoint with the work center and window', async () => {
    const expected = snapshot();
    getMock.mockResolvedValue({ data: expected });

    const result = await reliabilityService.getSnapshot({
      machineId: 'machine-1',
      fromUtc: expected.fromUtc,
      toUtc: expected.toUtc
    });

    expect(result).toEqual(expected);
    expect(getMock).toHaveBeenCalledOnce();
    expect(getMock).toHaveBeenCalledWith('/api/reliability/snapshot', {
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
      reliabilityService.getSnapshot({ machineId: 'foreign', fromUtc: 'a', toUtc: 'b' })
    ).rejects.toBe(failure);
  });

  it('getTrend queries the trend endpoint with the work center, window and bucket', async () => {
    const expected = trend();
    getMock.mockResolvedValue({ data: expected });

    const result = await reliabilityService.getTrend({
      machineId: 'machine-1',
      fromUtc: expected.fromUtc,
      toUtc: expected.toUtc,
      bucket: ReliabilityTrendBucket.Week
    });

    expect(result).toEqual(expected);
    expect(getMock).toHaveBeenCalledWith('/api/reliability/trend', {
      params: {
        machineId: 'machine-1',
        fromUtc: expected.fromUtc,
        toUtc: expected.toUtc,
        bucket: ReliabilityTrendBucket.Week
      }
    });
  });

  it('getTrend propagates API errors (e.g. 400 for an unknown bucket) to the caller', async () => {
    const failure = new Error('Request failed with status code 400');
    getMock.mockRejectedValue(failure);

    await expect(
      reliabilityService.getTrend({
        machineId: 'machine-1',
        fromUtc: 'a',
        toUtc: 'b',
        bucket: ReliabilityTrendBucket.Day
      })
    ).rejects.toBe(failure);
  });

  it('getFleet queries the fleet endpoint with the shared window', async () => {
    const expected = [fleetRow()];
    getMock.mockResolvedValue({ data: expected });
    const fromUtc = new Date('2026-09-24T06:00:00Z').toISOString();
    const toUtc = new Date('2026-10-08T06:00:00Z').toISOString();

    const result = await reliabilityService.getFleet({ fromUtc, toUtc });

    expect(result).toEqual(expected);
    expect(getMock).toHaveBeenCalledWith('/api/reliability/fleet', {
      params: { fromUtc, toUtc }
    });
  });

  it('getFleet propagates API errors to the caller', async () => {
    const failure = new Error('Request failed with status code 400');
    getMock.mockRejectedValue(failure);

    await expect(
      reliabilityService.getFleet({ fromUtc: 'b', toUtc: 'a' })
    ).rejects.toBe(failure);
  });
});

describe('validateReliabilityWindow', () => {
  it('accepts a valid window inside the 93-day cap', () => {
    expect(validateReliabilityWindow('2026-09-24T06:00', '2026-09-24T14:00')).toBeNull();
  });

  it('rejects reversed or equal dates before any request', () => {
    expect(validateReliabilityWindow('2026-09-24T14:00', '2026-09-24T06:00')).toBe('reversed');
    expect(validateReliabilityWindow('2026-09-24T06:00', '2026-09-24T06:00')).toBe('reversed');
  });

  it('rejects windows over 93 days mirroring the API cap', () => {
    expect(validateReliabilityWindow('2026-06-01T00:00', '2026-09-24T00:01')).toBe('tooLong');
  });

  it('accepts exactly 93 days', () => {
    const from = new Date('2026-06-23T00:00:00Z');
    const to = new Date(from.getTime() + 93 * 24 * 3600 * 1000);
    expect(validateReliabilityWindow(from.toISOString(), to.toISOString())).toBeNull();
  });

  it('rejects empty or unparseable inputs', () => {
    expect(validateReliabilityWindow('', '2026-09-24T14:00')).toBe('missing');
    expect(validateReliabilityWindow('2026-09-24T06:00', '')).toBe('missing');
    expect(validateReliabilityWindow('not-a-date', '2026-09-24T14:00')).toBe('missing');
  });
});

describe('validateReliabilityBucket', () => {
  it('accepts Day and Week mirroring the API buckets', () => {
    expect(validateReliabilityBucket(ReliabilityTrendBucket.Day)).toBeNull();
    expect(validateReliabilityBucket(ReliabilityTrendBucket.Week)).toBeNull();
  });

  it('accepts bucket names case-insensitively like the backend normalizes', () => {
    expect(validateReliabilityBucket('day')).toBeNull();
    expect(validateReliabilityBucket('WEEK')).toBeNull();
  });

  it('rejects empty input as missing', () => {
    expect(validateReliabilityBucket(null)).toBe('missing');
    expect(validateReliabilityBucket(undefined)).toBe('missing');
    expect(validateReliabilityBucket('')).toBe('missing');
    expect(validateReliabilityBucket('   ')).toBe('missing');
  });

  it('rejects unknown buckets mirroring the API 400', () => {
    expect(validateReliabilityBucket('Month')).toBe('unknown');
    expect(validateReliabilityBucket('daily')).toBe('unknown');
  });
});

describe('hasNullReliability', () => {
  it('is true for a zero-failure window with null MTBF/MTTR', () => {
    expect(
      hasNullReliability(snapshot({ failureCount: 0, mtbfMinutes: null, mttrMinutes: null }))
    ).toBe(true);
  });

  it('is false when failures were recorded', () => {
    expect(hasNullReliability(snapshot())).toBe(false);
  });

  it('is false without a snapshot', () => {
    expect(hasNullReliability(null)).toBe(false);
  });
});

describe('formatNullableMinutes', () => {
  it('formats minute KPIs with the min suffix', () => {
    expect(formatNullableMinutes(420)).toContain('420');
    expect(formatMinutes(60)).toContain('60');
  });

  it('renders null KPIs as an em dash, never as zeros', () => {
    expect(formatNullableMinutes(null)).toBe('—');
    expect(formatNullableMinutes(undefined)).toBe('—');
    expect(formatNullableMinutes(Number.NaN)).toBe('—');
  });
});
