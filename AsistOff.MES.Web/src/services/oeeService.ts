import http from './http';

/**
 * Trend bucket granularity accepted by `GET /api/oee/trend`. The backend
 * normalizes the value to `Day` or `Week` and rejects anything else with 400.
 */
export const OeeBucket = {
  Day: 'Day',
  Week: 'Week'
} as const;
export type OeeBucket = typeof OeeBucket[keyof typeof OeeBucket];

/**
 * OEE snapshot contract. Mirrors `OeeSnapshotResponse` from the backend:
 * echoed inputs, the four nullable factors (null when they cannot be
 * computed — never zeros), per-factor computed flags and the component
 * totals the factors were derived from.
 */
export interface OeeSnapshot {
  machineId: string;
  fromUtc: string;
  toUtc: string;
  idealCycleTimeSeconds: number;
  availability: number | null;
  performance: number | null;
  quality: number | null;
  oee: number | null;
  availabilityComputed: boolean;
  performanceComputed: boolean;
  qualityComputed: boolean;
  plannedProductionTimeMinutes: number;
  runTimeMinutes: number;
  downtimeMinutes: number;
  totalCount: number;
  goodCount: number;
  scrapCount: number;
}

/** OEE trend contract: normalized bucket plus one snapshot per bucket. */
export interface OeeTrend {
  machineId: string;
  fromUtc: string;
  toUtc: string;
  idealCycleTimeSeconds: number;
  bucket: string;
  buckets: OeeSnapshot[];
}

/** One downtime Pareto row: overlap minutes and share of total downtime. */
export interface OeeDowntimeParetoEntry {
  reasonCodeId: string;
  code: string | null;
  displayName: string | null;
  minutes: number;
  share: number;
}

/** One scrap Pareto row: quantity and share of total scrap. */
export interface OeeScrapParetoEntry {
  reasonCodeId: string;
  code: string | null;
  displayName: string | null;
  quantity: number;
  share: number;
}

/** Loss Pareto contract: window totals plus both Pareto lists. */
export interface OeeLosses {
  machineId: string;
  fromUtc: string;
  toUtc: string;
  totalDowntimeMinutes: number;
  downtimePareto: OeeDowntimeParetoEntry[];
  totalScrapQuantity: number;
  scrapPareto: OeeScrapParetoEntry[];
}

export interface GetOeeSnapshotQuery {
  machineId: string;
  fromUtc: string;
  toUtc: string;
  idealCycleTimeSeconds: number;
}

export interface GetOeeTrendQuery extends GetOeeSnapshotQuery {
  bucket: OeeBucket;
}

export interface GetOeeLossesQuery {
  machineId: string;
  fromUtc: string;
  toUtc: string;
}

const BASE = '/api/oee';

export const oeeService = {
  /** Per-Work Center OEE snapshot over a UTC time window. */
  async getSnapshot(query: GetOeeSnapshotQuery): Promise<OeeSnapshot> {
    const { data } = await http.get<OeeSnapshot>(`${BASE}/snapshot`, { params: { ...query } });
    return data;
  },
  /** Per-Work Center OEE trend: one snapshot per Day or Week bucket. */
  async getTrend(query: GetOeeTrendQuery): Promise<OeeTrend> {
    const { data } = await http.get<OeeTrend>(`${BASE}/trend`, { params: { ...query } });
    return data;
  },
  /** Per-Work Center loss Pareto: downtime minutes and scrap by reason code. */
  async getLosses(query: GetOeeLossesQuery): Promise<OeeLosses> {
    const { data } = await http.get<OeeLosses>(`${BASE}/losses`, { params: { ...query } });
    return data;
  }
};

/**
 * Formats an OEE factor (0..1 ratio) as a percentage with one decimal.
 * Null/undefined factors render as an em dash — the backend returns null
 * (never zeros) when a factor cannot be computed.
 */
export function formatOeeFactor(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return '—';
  return `${(value * 100).toFixed(1)}%`;
}

/** Formats a Pareto share (0..1 ratio) as a percentage with one decimal. */
export function formatOeeShare(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return '—';
  return `${(value * 100).toFixed(1)}%`;
}

/**
 * True when none of the snapshot factors could be computed — e.g. a window
 * with no planned calendar time. The dashboard shows the null-factor notice
 * instead of zeros in that case.
 */
export function hasNullFactors(snapshot: OeeSnapshot | null): boolean {
  if (!snapshot) return false;
  return !snapshot.availabilityComputed && !snapshot.performanceComputed && !snapshot.qualityComputed;
}

/** Human label for a Pareto row: display name, falling back to code. */
export function paretoLabel(entry: { displayName: string | null; code: string | null }): string {
  return entry.displayName ?? entry.code ?? '—';
}
