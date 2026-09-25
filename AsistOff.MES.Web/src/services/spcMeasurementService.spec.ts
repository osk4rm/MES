import { beforeEach, describe, expect, it, vi } from 'vitest';
import http from './http';
import {
  spcMeasurementService,
  type SpcMeasurementChartResponse,
  type SpcMeasurementResponse
} from './spcMeasurementService';

vi.mock('./http', () => ({
  default: {
    get: vi.fn()
  }
}));

const getMock = vi.mocked(http.get);

function measurement(overrides: Partial<SpcMeasurementResponse> = {}): SpcMeasurementResponse {
  return {
    id: 'measurement-1',
    characteristicId: 'characteristic-1',
    value: 10.5,
    measuredAt: new Date('2026-09-24T10:00:00Z').toISOString(),
    notes: null,
    createdAt: new Date('2026-09-24T10:00:00Z').toISOString(),
    updatedAt: null,
    ...overrides
  };
}

function chart(overrides: Partial<SpcMeasurementChartResponse> = {}): SpcMeasurementChartResponse {
  return {
    characteristicId: 'characteristic-1',
    nominalValue: 10,
    lowerSpecLimit: 9,
    upperSpecLimit: 11,
    lowerControlLimit: 9.5,
    upperControlLimit: 10.5,
    points: [
      {
        id: 'measurement-1',
        value: 10.5,
        measuredAt: new Date('2026-09-24T10:00:00Z').toISOString(),
        isOutOfControl: false,
        isOutOfSpec: false
      }
    ],
    totalCount: 1,
    outOfControlCount: 0,
    outOfSpecCount: 0,
    ...overrides
  };
}

describe('spcMeasurementService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('browse queries the log endpoint scoped to the characteristic with paging', async () => {
    const payload = { items: [measurement()], totalCount: 1, totalPages: 1 };
    getMock.mockResolvedValue({ data: payload });

    const result = await spcMeasurementService.browse({
      characteristicId: 'characteristic-1',
      pageNumber: 1,
      pageSize: 200
    });

    expect(result).toEqual(payload);
    expect(getMock).toHaveBeenCalledOnce();
    expect(getMock).toHaveBeenCalledWith('/api/spc-measurements', {
      params: {
        characteristicId: 'characteristic-1',
        pageNumber: 1,
        pageSize: 200
      }
    });
  });

  it('browse forwards the date range so the log and the chart stay in sync', async () => {
    const payload = { items: [measurement()], totalCount: 1, totalPages: 1 };
    getMock.mockResolvedValue({ data: payload });

    await spcMeasurementService.browse({
      characteristicId: 'characteristic-1',
      from: new Date('2026-09-24T06:00:00Z').toISOString(),
      to: new Date('2026-09-24T14:00:00Z').toISOString(),
      pageNumber: 1,
      pageSize: 200
    });

    expect(getMock).toHaveBeenCalledWith('/api/spc-measurements', {
      params: {
        characteristicId: 'characteristic-1',
        from: new Date('2026-09-24T06:00:00Z').toISOString(),
        to: new Date('2026-09-24T14:00:00Z').toISOString(),
        pageNumber: 1,
        pageSize: 200
      }
    });
  });

  it('getChart queries the chart endpoint with the characteristic and the range', async () => {
    const payload = chart();
    getMock.mockResolvedValue({ data: payload });

    const result = await spcMeasurementService.getChart({
      characteristicId: 'characteristic-1',
      from: new Date('2026-09-24T06:00:00Z').toISOString(),
      to: new Date('2026-09-24T14:00:00Z').toISOString()
    });

    expect(result).toEqual(payload);
    expect(getMock).toHaveBeenCalledOnce();
    expect(getMock).toHaveBeenCalledWith('/api/spc-measurements/chart', {
      params: {
        characteristicId: 'characteristic-1',
        from: new Date('2026-09-24T06:00:00Z').toISOString(),
        to: new Date('2026-09-24T14:00:00Z').toISOString()
      }
    });
  });

  it('getChart drops empty range bounds so an unbounded range loads everything', async () => {
    const payload = chart({ points: [] });
    getMock.mockResolvedValue({ data: payload });

    await spcMeasurementService.getChart({ characteristicId: 'characteristic-1' });

    expect(getMock).toHaveBeenCalledWith('/api/spc-measurements/chart', {
      params: { characteristicId: 'characteristic-1' }
    });
  });

  it('get queries a single measurement by id', async () => {
    const payload = measurement();
    getMock.mockResolvedValue({ data: payload });

    const result = await spcMeasurementService.get('measurement-1');

    expect(result).toEqual(payload);
    expect(getMock).toHaveBeenCalledWith('/api/spc-measurements/measurement-1');
  });

  it('propagates API errors (404 unknown/cross-tenant characteristic, 401 unauthenticated) to the caller', async () => {
    const notFound = new Error('Request failed with status code 404');
    getMock.mockRejectedValue(notFound);

    await expect(spcMeasurementService.browse({ characteristicId: 'foreign' })).rejects.toBe(notFound);
    await expect(spcMeasurementService.getChart({ characteristicId: 'foreign' })).rejects.toBe(notFound);

    const unauthorized = new Error('Request failed with status code 401');
    getMock.mockRejectedValue(unauthorized);

    await expect(spcMeasurementService.get('measurement-1')).rejects.toBe(unauthorized);
  });
});
