import { beforeEach, describe, expect, it, vi } from 'vitest';
import http from './http';
import {
  opcUaConnectionService,
  type OpcUaConnectionStatusEntry,
  type OpcUaConnectionStatusResponse
} from './opcUaConnectionService';

vi.mock('./http', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn()
  }
}));

const getMock = vi.mocked(http.get);
const postMock = vi.mocked(http.post);

function entry(overrides: Partial<OpcUaConnectionStatusEntry> = {}): OpcUaConnectionStatusEntry {
  return {
    connectionId: 'conn-1',
    machineId: 'machine-1',
    endpointUrl: 'opc.tcp://plc-a:4840',
    isEnabled: true,
    lastSeenAtUtc: new Date('2026-09-24T12:00:00Z').toISOString(),
    lastError: null,
    isLive: true,
    totalTags: 3,
    reportingTags: 1,
    staleTags: 2,
    ...overrides
  };
}

function status(overrides: Partial<OpcUaConnectionStatusResponse> = {}): OpcUaConnectionStatusResponse {
  return {
    connections: [entry()],
    totalCount: 1,
    liveCount: 1,
    staleCount: 0,
    disabledCount: 0,
    ...overrides
  };
}

describe('opcUaConnectionService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('getStatus queries the status endpoint without params when no filter is given', async () => {
    const payload = status();
    getMock.mockResolvedValue({ data: payload });

    const result = await opcUaConnectionService.getStatus();

    expect(result).toEqual(payload);
    expect(getMock).toHaveBeenCalledOnce();
    expect(getMock).toHaveBeenCalledWith('/api/opcua-connections/status', {
      params: undefined
    });
  });

  it('getStatus forwards the machineId filter for the deep-linked panel', async () => {
    const payload = status({ connections: [entry({ machineId: 'machine-9' })] });
    getMock.mockResolvedValue({ data: payload });

    const result = await opcUaConnectionService.getStatus('machine-9');

    expect(result).toEqual(payload);
    expect(getMock).toHaveBeenCalledWith('/api/opcua-connections/status', {
      params: { machineId: 'machine-9' }
    });
  });

  it('test posts to the slice-1 test action and returns reachability', async () => {
    const reachable = {
      id: 'conn-1',
      endpointUrl: 'opc.tcp://plc-a:4840',
      reachable: true,
      checkedAt: new Date('2026-09-24T12:00:00Z').toISOString()
    };
    postMock.mockResolvedValue({ data: reachable });

    const result = await opcUaConnectionService.test('conn-1');

    expect(result).toEqual(reachable);
    expect(postMock).toHaveBeenCalledWith('/api/opcua-connections/conn-1/test');
  });

  it('propagates API errors (404 cross-tenant, 401 unauthenticated) to the caller', async () => {
    const notFound = new Error('Request failed with status code 404');
    getMock.mockRejectedValue(notFound);

    await expect(opcUaConnectionService.getStatus('foreign-machine')).rejects.toBe(notFound);

    const unauthorized = new Error('Request failed with status code 401');
    postMock.mockRejectedValue(unauthorized);

    await expect(opcUaConnectionService.test('conn-1')).rejects.toBe(unauthorized);
  });
});
