import http from './http';

export interface OpcUaConnectionStatusEntry {
  connectionId: string;
  machineId: string;
  endpointUrl: string;
  isEnabled: boolean;
  lastSeenAtUtc?: string | null;
  lastError?: string | null;
  isLive: boolean;
  totalTags: number;
  reportingTags: number;
  staleTags: number;
}

export interface OpcUaConnectionStatusResponse {
  connections: OpcUaConnectionStatusEntry[];
  totalCount: number;
  liveCount: number;
  staleCount: number;
  disabledCount: number;
}

export interface OpcUaConnectionTestResponse {
  id: string;
  endpointUrl: string;
  reachable: boolean;
  checkedAt: string;
}

const BASE = '/api/opcua-connections';

export const opcUaConnectionService = {
  async getStatus(machineId?: string): Promise<OpcUaConnectionStatusResponse> {
    const { data } = await http.get<OpcUaConnectionStatusResponse>(`${BASE}/status`, {
      params: machineId ? { machineId } : undefined
    });
    return data;
  },
  async test(id: string): Promise<OpcUaConnectionTestResponse> {
    const { data } = await http.post<OpcUaConnectionTestResponse>(`${BASE}/${id}/test`);
    return data;
  }
};
