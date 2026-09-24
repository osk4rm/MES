import { beforeEach, describe, expect, it, vi } from 'vitest';
import http from './http';
import {
  formatMinutes,
  formatNullableMinutes,
  hasNullReliability,
  reliabilityService,
  validateReliabilityWindow,
  type ReliabilitySnapshot
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
