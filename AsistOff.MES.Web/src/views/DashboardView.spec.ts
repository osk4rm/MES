import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import DashboardView from './DashboardView.vue';
import { productionOrderService, ProductionOrderStatus } from '../services/productionOrderService';
import { machineService } from '../services/machineService';
import { operatorService } from '../services/operatorService';
import { warehouseService } from '../services/warehouseService';

// Slice (2/3) F-01 + F-08: the post-login landing page reads live tenant
// totals (Released + InProgress Production Orders, Work Centers, Operators,
// Warehouses) behind the shared AppDataState precedence instead of hardcoded
// zero cards, and the KPI icon chips bind CSS token classes instead of
// script-side hex.

vi.mock('../services/productionOrderService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../services/productionOrderService')>();
  return { ...actual, productionOrderService: { browse: vi.fn() } };
});

vi.mock('../services/machineService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../services/machineService')>();
  return { ...actual, machineService: { browse: vi.fn() } };
});

vi.mock('../services/operatorService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../services/operatorService')>();
  return { ...actual, operatorService: { browse: vi.fn() } };
});

vi.mock('../services/warehouseService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../services/warehouseService')>();
  return { ...actual, warehouseService: { browse: vi.fn() } };
});

vi.mock('vue-i18n', () => ({
  useI18n: (): { t: (key: string) => string } => ({
    t: (key: string): string => key
  })
}));

const ordersMock = vi.mocked(productionOrderService.browse);
const machinesMock = vi.mocked(machineService.browse);
const operatorsMock = vi.mocked(operatorService.browse);
const warehousesMock = vi.mocked(warehouseService.browse);

function countPage(totalCount: number): { items: never[]; totalCount: number; totalPages: number } {
  return { items: [], totalCount, totalPages: totalCount > 0 ? 1 : 0 };
}

function seedHealthy(): void {
  ordersMock.mockImplementation(async (req) => {
    if (req.status === ProductionOrderStatus.Released) return countPage(5);
    if (req.status === ProductionOrderStatus.InProgress) return countPage(2);
    return countPage(0);
  });
  machinesMock.mockResolvedValue(countPage(3));
  operatorsMock.mockResolvedValue(countPage(4));
  warehousesMock.mockResolvedValue(countPage(2));
}

function mountDashboard(): VueWrapper {
  return mount(DashboardView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key }
    }
  }) as unknown as VueWrapper;
}

describe('DashboardView', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it('renders live KPI totals with active orders summed across Released and InProgress', async () => {
    seedHealthy();

    const wrapper = mountDashboard();
    await flushPromises();

    expect(ordersMock).toHaveBeenCalledWith(
      expect.objectContaining({ pageSize: 1, status: ProductionOrderStatus.Released })
    );
    expect(ordersMock).toHaveBeenCalledWith(
      expect.objectContaining({ pageSize: 1, status: ProductionOrderStatus.InProgress })
    );
    const text = wrapper.text();
    // 5 Released + 2 InProgress = 7 active orders; sibling counters read
    // their own browse totals.
    expect(text).toContain('7');
    expect(text).toContain('3');
    expect(text).toContain('4');
    expect(text).toContain('2');
    expect(text).toContain('dashboard.activeOrders');
    expect(text).toContain('dashboard.machines');
    expect(text).toContain('dashboard.operators');
    expect(text).toContain('dashboard.warehouses');
    // Every card carries the live signal (icon + text, never color-only).
    expect(text).toContain('dashboard.live');
  });

  it('binds token classes instead of inline hex on the KPI icon chips', async () => {
    seedHealthy();

    const wrapper = mountDashboard();
    await flushPromises();

    for (const cls of ['kpi__icon--orders', 'kpi__icon--machines', 'kpi__icon--operators', 'kpi__icon--warehouses']) {
      const chip = wrapper.find(`.${cls}`);
      expect(chip.exists()).toBe(true);
      expect(chip.attributes('style')).toBeUndefined();
    }
  });

  it('shows an error with retry when a count endpoint fails, and recovers on retry', async () => {
    seedHealthy();
    machinesMock.mockRejectedValueOnce(new Error('boom'));

    const wrapper = mountDashboard();
    await flushPromises();

    expect(wrapper.text()).toContain('boom');
    expect(wrapper.text()).not.toContain('dashboard.live');

    const retry = wrapper.findAll('button').find((b) => b.text().includes('common.retry'));
    expect(retry).toBeDefined();
    await retry?.trigger('click');
    await flushPromises();

    expect(machinesMock).toHaveBeenCalledTimes(2);
    expect(wrapper.text()).toContain('dashboard.live');
    expect(wrapper.text()).not.toContain('boom');
  });

  it('reads empty (not zero cards) for a tenant with no data', async () => {
    ordersMock.mockResolvedValue(countPage(0));
    machinesMock.mockResolvedValue(countPage(0));
    operatorsMock.mockResolvedValue(countPage(0));
    warehousesMock.mockResolvedValue(countPage(0));

    const wrapper = mountDashboard();
    await flushPromises();

    expect(wrapper.text()).toContain('dashboard.emptyTitle');
    expect(wrapper.text()).toContain('dashboard.emptyHint');
    expect(wrapper.text()).not.toContain('dashboard.live');
  });
});
