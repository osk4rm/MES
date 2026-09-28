import { computed, ref } from 'vue';
import { AndonSignalStatus } from '../services/andonSignalService';
import { DowntimeEventStatus } from '../services/downtimeEventService';
import { LotStatus } from '../services/lotService';
import { ProductionOrderStatus } from '../services/productionOrderService';

// Shared presentation helpers for operator-facing (shopfloor) views
// (issue #331, slice 3/3): gloved-operation touch targets, Andon severity
// signaling that never relies on color alone, and a persisted density option
// so the same views stay usable on small shopfloor tablets and back-office
// desktops. All helpers are pure (except the tiny localStorage persistence
// in `useShopfloorDensity`) and covered by `useShopfloorDisplay.spec.ts`.
//
// Backend/i18n note: labels still come from `$t(...)` in the views; the
// helpers below only map statuses to badge variants + PrimeIcons glyphs.

/** Minimum touch-target edge in px for gloved operation on shopfloor views. */
export const SHOPFLOOR_MIN_TOUCH_PX = 44;

/** Tablet breakpoint (px) at which shopfloor views collapse to one column. */
export const SHOPFLOOR_TABLET_BREAKPOINT_PX = 1100;

export const ShopfloorDensity = {
  Comfortable: 'comfortable',
  Compact: 'compact'
} as const;
export type ShopfloorDensity = (typeof ShopfloorDensity)[keyof typeof ShopfloorDensity];

const DENSITY_STORAGE_KEY = 'shopfloor-density';

/** Normalizes an unknown persisted value to a known density (defaults to touch-friendly). */
export function resolveShopfloorDensity(value: unknown): ShopfloorDensity {
  return value === ShopfloorDensity.Compact ? ShopfloorDensity.Compact : ShopfloorDensity.Comfortable;
}

/** Density modifier class applied next to `shopfloor-view` on the view root. */
export function shopfloorDensityClass(density: ShopfloorDensity): string {
  return density === ShopfloorDensity.Compact ? 'shopfloor-view--compact' : 'shopfloor-view--comfortable';
}

/** Root classes for a shopfloor view: touch-target scope + density modifier. */
export function shopfloorViewClass(density: ShopfloorDensity): string[] {
  return ['shopfloor-view', shopfloorDensityClass(density)];
}

export interface StatusSignalMeta {
  variant: 'neutral' | 'primary' | 'success' | 'warning' | 'danger' | 'info' | 'idle';
  icon: string;
}

/**
 * Andon severity signaling: color (variant) + icon + text label. Active
 * signals must read as abnormal at a distance; acknowledged ones as
 * seen-but-open; anything else falls back to the active treatment so an
 * unknown status can never render as calm.
 */
export function andonSeverityMeta(status: number): StatusSignalMeta {
  if (status === AndonSignalStatus.Acknowledged) return { variant: 'warning', icon: 'pi pi-eye' };
  if (status === AndonSignalStatus.Resolved) return { variant: 'success', icon: 'pi pi-check-circle' };
  return { variant: 'danger', icon: 'pi pi-exclamation-triangle' };
}

/** Downtime event signaling: open reads as attention-needed, closed as calm. */
export function downtimeStatusMeta(status: number): StatusSignalMeta {
  if (status === DowntimeEventStatus.Closed) return { variant: 'idle', icon: 'pi pi-check-circle' };
  return { variant: 'warning', icon: 'pi pi-pause-circle' };
}

/** Lot status signaling for the lot lookup / genealogy views. */
export function lotStatusMeta(status: number): StatusSignalMeta {
  if (status === LotStatus.Available) return { variant: 'success', icon: 'pi pi-check-circle' };
  if (status === LotStatus.OnHold) return { variant: 'warning', icon: 'pi pi-pause' };
  if (status === LotStatus.Consumed) return { variant: 'info', icon: 'pi pi-info-circle' };
  if (status === LotStatus.Scrapped) return { variant: 'danger', icon: 'pi pi-trash' };
  if (status === LotStatus.Expired) return { variant: 'idle', icon: 'pi pi-clock' };
  return { variant: 'idle', icon: 'pi pi-info-circle' };
}

/** Production Order lifecycle signaling shared by dispatch and Confirmation views. */
export function productionOrderStatusMeta(status: number): StatusSignalMeta {
  if (status === ProductionOrderStatus.Planned) return { variant: 'info', icon: 'pi pi-calendar' };
  if (status === ProductionOrderStatus.Released) return { variant: 'success', icon: 'pi pi-play' };
  if (status === ProductionOrderStatus.InProgress) return { variant: 'warning', icon: 'pi pi-cog' };
  if (status === ProductionOrderStatus.Completed) return { variant: 'primary', icon: 'pi pi-check' };
  if (status === ProductionOrderStatus.Closed) return { variant: 'idle', icon: 'pi pi-lock' };
  // Held orders read as attention-needed but distinct from in-progress work
  // (pause glyph, never color alone).
  if (status === ProductionOrderStatus.OnHold) return { variant: 'warning', icon: 'pi pi-pause' };
  return { variant: 'info', icon: 'pi pi-info-circle' };
}

/**
 * Per-view density state, persisted to `localStorage` so operators keep
 * their preferred ergonomics across sessions. Each view owns its own ref
 * (initialized from storage) so the helper stays trivially testable.
 */
export function useShopfloorDensity() {
  let initial: ShopfloorDensity = ShopfloorDensity.Comfortable;
  try {
    initial = resolveShopfloorDensity(localStorage.getItem(DENSITY_STORAGE_KEY));
  } catch {
    initial = ShopfloorDensity.Comfortable;
  }
  const density = ref<ShopfloorDensity>(initial);
  const viewClass = computed(() => shopfloorViewClass(density.value));

  function setDensity(next: ShopfloorDensity): void {
    density.value = next;
    try {
      localStorage.setItem(DENSITY_STORAGE_KEY, next);
    } catch {
      /* private-mode storage — density still applies for this session */
    }
  }

  function toggleDensity(): void {
    setDensity(density.value === ShopfloorDensity.Comfortable ? ShopfloorDensity.Compact : ShopfloorDensity.Comfortable);
  }

  return { density, viewClass, setDensity, toggleDensity };
}
