import { beforeEach, describe, expect, it } from 'vitest';
import {
  SHOPFLOOR_MIN_TOUCH_PX,
  ShopfloorDensity,
  andonSeverityMeta,
  downtimeStatusMeta,
  lotStatusMeta,
  productionOrderStatusMeta,
  resolveShopfloorDensity,
  shopfloorDensityClass,
  shopfloorViewClass,
  useShopfloorDensity
} from './useShopfloorDisplay';
import { AndonSignalStatus } from '../services/andonSignalService';
import { DowntimeEventStatus } from '../services/downtimeEventService';
import { LotStatus } from '../services/lotService';
import { ProductionOrderStatus } from '../services/productionOrderService';

// Covers the slice 3/3 presentation helpers (issue #331): gloved-operation
// touch minimum, Andon severity signaling that never relies on color alone,
// and the persisted density option.
describe('shopfloor display helpers (issue #331)', () => {
  it('requires a 44px minimum touch target for gloved operation', () => {
    expect(SHOPFLOOR_MIN_TOUCH_PX).toBeGreaterThanOrEqual(44);
  });

  it('resolves only an explicit compact value to compact density', () => {
    expect(resolveShopfloorDensity('compact')).toBe(ShopfloorDensity.Compact);
    expect(resolveShopfloorDensity('comfortable')).toBe(ShopfloorDensity.Comfortable);
    expect(resolveShopfloorDensity(null)).toBe(ShopfloorDensity.Comfortable);
    expect(resolveShopfloorDensity(undefined)).toBe(ShopfloorDensity.Comfortable);
    expect(resolveShopfloorDensity('')).toBe(ShopfloorDensity.Comfortable);
    expect(resolveShopfloorDensity('COMPACT')).toBe(ShopfloorDensity.Comfortable);
    expect(resolveShopfloorDensity(42)).toBe(ShopfloorDensity.Comfortable);
  });

  it('maps densities to distinct root modifier classes', () => {
    expect(shopfloorDensityClass(ShopfloorDensity.Comfortable)).toBe('shopfloor-view--comfortable');
    expect(shopfloorDensityClass(ShopfloorDensity.Compact)).toBe('shopfloor-view--compact');
  });

  it('builds the shopfloor view root class with the touch scope first', () => {
    expect(shopfloorViewClass(ShopfloorDensity.Comfortable)).toEqual([
      'shopfloor-view',
      'shopfloor-view--comfortable'
    ]);
    expect(shopfloorViewClass(ShopfloorDensity.Compact)).toEqual([
      'shopfloor-view',
      'shopfloor-view--compact'
    ]);
  });

  it('maps Andon severities to distinct color-plus-icon signals', () => {
    expect(andonSeverityMeta(AndonSignalStatus.Active)).toEqual({
      variant: 'danger',
      icon: 'pi pi-exclamation-triangle'
    });
    expect(andonSeverityMeta(AndonSignalStatus.Acknowledged)).toEqual({
      variant: 'warning',
      icon: 'pi pi-eye'
    });
    expect(andonSeverityMeta(AndonSignalStatus.Resolved)).toEqual({
      variant: 'success',
      icon: 'pi pi-check-circle'
    });
  });

  it('fails safe to the abnormal Andon treatment for unknown statuses', () => {
    for (const unknown of [0, -1, 99]) {
      const meta = andonSeverityMeta(unknown);
      expect(meta.variant).toBe('danger');
      expect(meta.icon.length).toBeGreaterThan(0);
    }
  });

  it('maps downtime states to attention-needed vs calm signals', () => {
    expect(downtimeStatusMeta(DowntimeEventStatus.Open)).toEqual({
      variant: 'warning',
      icon: 'pi pi-pause-circle'
    });
    expect(downtimeStatusMeta(DowntimeEventStatus.Closed)).toEqual({
      variant: 'idle',
      icon: 'pi pi-check-circle'
    });
    expect(downtimeStatusMeta(99).variant).toBe('warning');
  });

  it('maps every lot status to a distinct icon signal', () => {
    const icons = new Set([
      lotStatusMeta(LotStatus.Available).icon,
      lotStatusMeta(LotStatus.OnHold).icon,
      lotStatusMeta(LotStatus.Consumed).icon,
      lotStatusMeta(LotStatus.Scrapped).icon,
      lotStatusMeta(LotStatus.Expired).icon
    ]);
    expect(icons.size).toBe(5);
    expect(lotStatusMeta(LotStatus.Available).variant).toBe('success');
    expect(lotStatusMeta(LotStatus.Scrapped).variant).toBe('danger');
    // Unknown lots stay neutral — never a false calm "available".
    expect(lotStatusMeta(99).variant).toBe('idle');
  });

  it('maps every Production Order state to a distinct icon signal', () => {
    const icons = new Set([
      productionOrderStatusMeta(ProductionOrderStatus.Planned).icon,
      productionOrderStatusMeta(ProductionOrderStatus.Released).icon,
      productionOrderStatusMeta(ProductionOrderStatus.InProgress).icon,
      productionOrderStatusMeta(ProductionOrderStatus.Completed).icon,
      productionOrderStatusMeta(ProductionOrderStatus.Closed).icon,
      productionOrderStatusMeta(ProductionOrderStatus.OnHold).icon
    ]);
    expect(icons.size).toBe(6);
    expect(productionOrderStatusMeta(ProductionOrderStatus.Released).variant).toBe('success');
    expect(productionOrderStatusMeta(ProductionOrderStatus.InProgress).variant).toBe('warning');
  });

  it('signals held orders as attention-needed with a pause glyph', () => {
    expect(productionOrderStatusMeta(ProductionOrderStatus.OnHold)).toEqual({
      variant: 'warning',
      icon: 'pi pi-pause'
    });
  });

  describe('useShopfloorDensity', () => {
    beforeEach(() => {
      localStorage.clear();
    });

    it('defaults to the touch-friendly density when nothing is stored', () => {
      const { density, viewClass } = useShopfloorDensity();

      expect(density.value).toBe(ShopfloorDensity.Comfortable);
      expect(viewClass.value).toEqual(['shopfloor-view', 'shopfloor-view--comfortable']);
    });

    it('restores the persisted compact density', () => {
      localStorage.setItem('shopfloor-density', 'compact');

      const { density, viewClass } = useShopfloorDensity();

      expect(density.value).toBe(ShopfloorDensity.Compact);
      expect(viewClass.value).toEqual(['shopfloor-view', 'shopfloor-view--compact']);
    });

    it('treats a corrupt stored value as comfortable', () => {
      localStorage.setItem('shopfloor-density', 'roomy');

      expect(useShopfloorDensity().density.value).toBe(ShopfloorDensity.Comfortable);
    });

    it('toggles and persists the density', () => {
      const { density, toggleDensity, setDensity } = useShopfloorDensity();

      toggleDensity();
      expect(density.value).toBe(ShopfloorDensity.Compact);
      expect(localStorage.getItem('shopfloor-density')).toBe('compact');

      setDensity(ShopfloorDensity.Comfortable);
      expect(density.value).toBe(ShopfloorDensity.Comfortable);
      expect(localStorage.getItem('shopfloor-density')).toBe('comfortable');
    });
  });
});
