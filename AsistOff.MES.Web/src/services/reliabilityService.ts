import http from './http';

/**
 * Reliability snapshot contract. Mirrors `ReliabilitySnapshotResponse` from
 * the backend: echoed inputs, failure/repair counts, window/uptime/downtime
 * totals and the nullable MTBF/MTTR/average-repair KPIs (null when they
 * cannot be computed — never zeros).
 */
export interface ReliabilitySnapshot {
  machineId: string;
  fromUtc: string;
  toUtc: string;
  failureCount: number;
  repairCount: number;
  windowMinutes: number;
  uptimeMinutes: number;
  totalDowntimeMinutes: number;
  mtbfMinutes: number | null;
  mttrMinutes: number | null;
  avgRepairMinutes: number | null;
}

export interface GetReliabilitySnapshotQuery {
  machineId: string;
  fromUtc: string;
  toUtc: string;
}

/** API window cap mirrored from `GetReliabilitySnapshotValidator` (93 days). */
export const MAX_RELIABILITY_WINDOW_DAYS = 93;

/**
 * Preset ranges offered by the reliability dashboard. `Custom` keeps the
 * manually edited From/To inputs untouched.
 */
export const ReliabilityPreset = {
  Last8Hours: 'last8h',
  Last24Hours: 'last24h',
  Last7Days: 'last7d',
  Last30Days: 'last30d',
  Custom: 'custom'
} as const;
export type ReliabilityPreset = typeof ReliabilityPreset[keyof typeof ReliabilityPreset];

export type ReliabilityWindowError = 'missing' | 'reversed' | 'tooLong';

const BASE = '/api/reliability';

export const reliabilityService = {
  /** Per-Work Center MTBF/MTTR snapshot over a UTC time window. */
  async getSnapshot(query: GetReliabilitySnapshotQuery): Promise<ReliabilitySnapshot> {
    const { data } = await http.get<ReliabilitySnapshot>(`${BASE}/snapshot`, { params: { ...query } });
    return data;
  }
};

/**
 * Client-side window validation mirroring the API cap: reversed or equal
 * dates are `reversed`, windows over 93 days are `tooLong`, unparseable or
 * empty inputs are `missing`. Returns null when the window is valid so the
 * view can reject illegal input before any request.
 */
export function validateReliabilityWindow(
  fromInput: string,
  toInput: string
): ReliabilityWindowError | null {
  if (!fromInput || !toInput) return 'missing';
  const from = new Date(fromInput);
  const to = new Date(toInput);
  if (Number.isNaN(from.getTime()) || Number.isNaN(to.getTime())) return 'missing';
  if (from >= to) return 'reversed';
  const days = (to.getTime() - from.getTime()) / (1000 * 60 * 60 * 24);
  if (days > MAX_RELIABILITY_WINDOW_DAYS) return 'tooLong';
  return null;
}

/**
 * True when the window holds zero failures: MTBF and MTTR are null by
 * contract (never zeros) and the dashboard shows the null-state notice.
 */
export function hasNullReliability(snapshot: ReliabilitySnapshot | null): boolean {
  if (!snapshot) return false;
  return snapshot.failureCount === 0 && snapshot.mtbfMinutes === null && snapshot.mttrMinutes === null;
}

/** Formats a minute total with at most one decimal, e.g. `480 min`. */
export function formatMinutes(value: number): string {
  if (!Number.isFinite(value)) return '—';
  return `${new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 }).format(value)} min`;
}

/**
 * Formats a nullable minute KPI: null/NaN renders as an em dash — the
 * backend returns null (never zeros) when a KPI cannot be computed.
 */
export function formatNullableMinutes(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return '—';
  return formatMinutes(value);
}
