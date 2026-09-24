import http from './http';

/**
 * One lot reached by transitive traceability traversal. Mirrors
 * LotTraceabilityNodeResponse from the backend: the edge data that led to
 * the lot (quantity, order, Work Center, timestamp) plus the lot identity
 * and the 1-based BFS depth (level from the root).
 */
export interface LotTraceabilityNode {
  lotId: string;
  lotCode: string;
  productId: string;
  depth: number;
  consumedQuantity: number;
  productionOrderId: string;
  productionOrderCode: string;
  machineId: string;
  reportedByOperatorId?: string | null;
  occurredAt: string;
}

/**
 * Transitive closure of lots reachable from a root lot. At most 500 nodes
 * are returned; `truncated` is set when more nodes were reachable.
 */
export interface LotTraceabilityResponse {
  rootLotId: string;
  rootLotCode: string;
  nodes: LotTraceabilityNode[];
  truncated: boolean;
}

export const LotGenealogyDepth = {
  min: 1,
  max: 10,
  default: 5
} as const;

/**
 * Normalizes a requested traversal depth so an invalid depth is never sent
 * to the API (the backend rejects maxDepth outside 1..10 with 400).
 */
export function normalizeGenealogyDepth(depth: number | null | undefined): number {
  if (typeof depth !== 'number' || !Number.isFinite(depth)) return LotGenealogyDepth.default;
  const rounded = Math.floor(depth);
  if (rounded < LotGenealogyDepth.min) return LotGenealogyDepth.min;
  if (rounded > LotGenealogyDepth.max) return LotGenealogyDepth.max;
  return rounded;
}

const BASE = '/api/lot-genealogy';

export const lotGenealogyService = {
  /** Upstream (where-from) traceability: lots consumed to produce `lotId`. */
  async getUpstream(lotId: string, maxDepth?: number | null): Promise<LotTraceabilityResponse> {
    const { data } = await http.get<LotTraceabilityResponse>(`${BASE}/upstream/${lotId}`, {
      params: { maxDepth: normalizeGenealogyDepth(maxDepth) }
    });
    return data;
  },
  /** Downstream (where-used) traceability: lots produced from `lotId`. */
  async getDownstream(lotId: string, maxDepth?: number | null): Promise<LotTraceabilityResponse> {
    const { data } = await http.get<LotTraceabilityResponse>(`${BASE}/downstream/${lotId}`, {
      params: { maxDepth: normalizeGenealogyDepth(maxDepth) }
    });
    return data;
  }
};
