import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import OperatorPanelView from './OperatorPanelView.vue';
import { operatorQueueService, type OperatorShiftQueue } from '../../services/operatorQueueService';
import { ProductionOrderStatus } from '../../services/productionOrderService';
import { productionConfirmationService } from '../../services/productionConfirmationService';
import { scrapEventService } from '../../services/scrapEventService';
import { downtimeEventService } from '../../services/downtimeEventService';
import {
  AndonSignalCategory,
  andonSignalService
} from '../../services/andonSignalService';
import { machineService } from '../../services/machineService';
import { operatorService } from '../../services/operatorService';
import {
  ReasonCodeCategory,
  reasonCodeService,
  type BrowseReasonCodesRequest
} from '../../services/reasonCodeService';
import { useToastStore } from '../../stores/toastStore';

// Closes the verifier gaps on PR #342 (issue #336, TESTS_INSUFFICIENT):
// the service spec only proved the GET queue call plus pure helpers, and the
// i18n spec only scanned view source text — nothing mounted the panel. These
// component tests prove the rendered contract: next-up badge plus remaining
// quantities plus icon-plus-text Andon groups, claim on a Released order
// persisting a Confirmation (the claim transition — no Release button, the
// queue only ever contains Released/InProgress rows), confirm on an
// InProgress order, scrap with a Reason code, downtime start with a Reason
// code, Andon acknowledge with reload, off-shift empty state, lookup pageSize
// caps, and the tablet single-column CSS (viewport evidence short of the
// deferred Playwright run, which belongs to the e2e stage).

vi.mock('../../services/operatorQueueService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/operatorQueueService')>();
  return {
    ...actual,
    operatorQueueService: {
      getQueue: vi.fn()
    }
  };
});

vi.mock('../../services/operatorService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/operatorService')>();
  return {
    ...actual,
    operatorService: {
      ...actual.operatorService,
      browse: vi.fn()
    }
  };
});

vi.mock('../../services/machineService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/machineService')>();
  return {
    ...actual,
    machineService: {
      ...actual.machineService,
      browse: vi.fn()
    }
  };
});

vi.mock('../../services/reasonCodeService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/reasonCodeService')>();
  return {
    ...actual,
    reasonCodeService: {
      ...actual.reasonCodeService,
      browse: vi.fn()
    }
  };
});

vi.mock('../../services/productionConfirmationService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/productionConfirmationService')>();
  return {
    ...actual,
    productionConfirmationService: {
      ...actual.productionConfirmationService,
      create: vi.fn()
    }
  };
});

vi.mock('../../services/scrapEventService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/scrapEventService')>();
  return {
    ...actual,
    scrapEventService: {
      ...actual.scrapEventService,
      create: vi.fn()
    }
  };
});

vi.mock('../../services/downtimeEventService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/downtimeEventService')>();
  return {
    ...actual,
    downtimeEventService: {
      ...actual.downtimeEventService,
      start: vi.fn()
    }
  };
});

vi.mock('../../services/andonSignalService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/andonSignalService')>();
  return {
    ...actual,
    andonSignalService: {
      ...actual.andonSignalService,
      acknowledge: vi.fn()
    }
  };
});

const mockPush = vi.fn();

vi.mock('vue-router', () => ({
  useRoute: (): { query: Record<string, unknown> } => ({ query: {} }),
  useRouter: (): { push: (...args: unknown[]) => void } => ({
    push: mockPush
  })
}));

vi.mock('vue-i18n', () => ({
  useI18n: (): {
    t: (key: string, params?: Record<string, unknown>) => string;
    tm: (key: string) => Record<string, string>;
  } => ({
    t: (key: string, params?: Record<string, unknown>): string =>
      params === undefined ? key : `${key} ${JSON.stringify(params)}`,
    tm: (key: string): Record<string, string> =>
      key === 'andon.categories'
        ? { '1': 'Downtime', '2': 'Quality', '3': 'Material', '4': 'Other' }
        : {}
  })
}));

const getQueueMock = vi.mocked(operatorQueueService.getQueue);
const operatorBrowseMock = vi.mocked(operatorService.browse);
const machineBrowseMock = vi.mocked(machineService.browse);
const reasonBrowseMock = vi.mocked(reasonCodeService.browse);
const confirmCreateMock = vi.mocked(productionConfirmationService.create);
const scrapCreateMock = vi.mocked(scrapEventService.create);
const downtimeStartMock = vi.mocked(downtimeEventService.start);
const acknowledgeMock = vi.mocked(andonSignalService.acknowledge);

function page<T>(items: T[]): { totalCount: number; totalPages: number; items: T[] } {
  return { totalCount: items.length, totalPages: items.length > 0 ? 1 : 0, items };
}

function queueFixture(): OperatorShiftQueue {
  return {
    operatorCode: 'OP-1',
    operatorId: 'operator-1',
    shift: {
      shiftId: 'shift-1',
      shiftCode: 'AM',
      shiftName: 'Morning',
      date: '2026-09-24',
      windowStartUtc: new Date('2026-09-24T06:00:00Z').toISOString(),
      windowEndUtc: new Date('2026-09-24T14:00:00Z').toISOString(),
      isOvernight: false
    },
    orders: [
      {
        id: 'order-1',
        code: 'OP-100',
        productId: 'product-1',
        productCode: 'PROD-1',
        plannedQuantity: 100,
        producedQuantity: 20,
        scrappedQuantity: 2,
        remainingQuantity: 78,
        machineId: 'machine-1',
        machineCode: 'M-1',
        machineName: 'Line 1',
        priority: 5,
        dueDate: new Date('2026-09-24T00:00:00Z').toISOString(),
        status: ProductionOrderStatus.Released
      },
      {
        id: 'order-2',
        code: 'OP-200',
        productId: 'product-1',
        productCode: 'PROD-1',
        plannedQuantity: 50,
        producedQuantity: 10,
        scrappedQuantity: 0,
        remainingQuantity: 40,
        machineId: 'machine-1',
        machineCode: 'M-1',
        machineName: 'Line 1',
        priority: 9,
        dueDate: null,
        status: ProductionOrderStatus.InProgress
      }
    ],
    activeSignals: [
      {
        id: 'signal-1',
        machineId: 'machine-1',
        machineCode: 'M-1',
        category: AndonSignalCategory.Downtime,
        severity: 'Downtime',
        raisedAt: new Date('2026-09-24T08:00:00Z').toISOString()
      }
    ]
  };
}

function seedLookups(): void {
  machineBrowseMock.mockResolvedValue(
    page([
      {
        id: 'machine-1',
        code: 'M-1',
        name: 'Line 1',
        isActive: true,
        capacity: 1,
        efficiencyFactor: 1
      }
    ])
  );
  operatorBrowseMock.mockResolvedValue(
    page([
      {
        id: 'operator-1',
        identifier: 'OP-1',
        firstName: 'Ada',
        lastName: 'Op',
        ratePerHour: 0
      }
    ])
  );
  reasonBrowseMock.mockImplementation(async (req: BrowseReasonCodesRequest) => {
    if (req.category === ReasonCodeCategory.Scrap) {
      return page([
        {
          id: 'reason-scrap-1',
          code: 'SCRAP-TOL',
          name: 'Tolerance over',
          category: ReasonCodeCategory.Scrap,
          isActive: true,
          sortIndex: 0
        }
      ]);
    }
    if (req.category === ReasonCodeCategory.Downtime) {
      return page([
        {
          id: 'reason-down-1',
          code: 'DT-MAINT',
          name: 'Breakdown',
          category: ReasonCodeCategory.Downtime,
          isActive: true,
          sortIndex: 0
        }
      ]);
    }
    return page([]);
  });
  confirmCreateMock.mockResolvedValue({ id: 'conf-1' } as never);
  scrapCreateMock.mockResolvedValue({ id: 'scrap-1' } as never);
  downtimeStartMock.mockResolvedValue({ id: 'down-1' } as never);
  acknowledgeMock.mockResolvedValue({ id: 'signal-1' } as never);
}

function mountPanel(): VueWrapper {
  return mount(OperatorPanelView, {
    global: {
      plugins: [createPinia()],
      // Interpolating $t keeps quantity/status params visible in the
      // rendered text so remaining quantities are assertable.
      mocks: {
        $t: (key: string, params?: Record<string, unknown>): string =>
          params === undefined ? key : `${key} ${JSON.stringify(params)}`
      },
      stubs: {
        // Render modal content inline so Teleport does not move it to
        // document.body (unreachable via wrapper.find in jsdom).
        AppModal: {
          props: ['open', 'title', 'size'],
          template: '<div v-if="open" class="modal-stub"><slot /><slot name="footer" /></div>'
        }
      }
    }
  }) as unknown as VueWrapper;
}

type ButtonWrapper = ReturnType<VueWrapper['findAll']>[number];

function findButton(wrapper: VueWrapper, label: string): ButtonWrapper | undefined {
  return wrapper.findAll('button').find((b) => b.text().includes(label));
}

function findButtonIn(wrapper: VueWrapper, rootTestId: string, label: string): ButtonWrapper | undefined {
  const root = wrapper.find(`[data-testid="${rootTestId}"]`);
  if (!root.exists()) return undefined;
  return root.findAll('button').find((b) => b.text().includes(label));
}

async function loadQueueFor(wrapper: VueWrapper, code = 'OP-1'): Promise<void> {
  const input = wrapper.find('input');
  expect(input.exists()).toBe(true);
  await input.setValue(code);
  const load = findButton(wrapper, 'operatorPanel.load');
  expect(load).toBeDefined();
  await load?.trigger('click');
  await flushPromises();
  await flushPromises();
}

// Raw Vue source via Vite (no node:fs so vue-tsc stays happy without
// @types/node). Used for the tablet-viewport CSS evidence below.
const viewSources = import.meta.glob<string>('./OperatorPanelView.vue', {
  eager: true,
  query: '?raw',
  import: 'default'
});

describe('OperatorPanelView', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    seedLookups();
    getQueueMock.mockResolvedValue(queueFixture());
  });

  it('renders the next-up badge, remaining quantities and icon-plus-text Andon groups', async () => {
    const wrapper = mountPanel();
    await flushPromises();
    await loadQueueFor(wrapper);

    expect(getQueueMock).toHaveBeenCalledWith('OP-1');

    const nextUp = wrapper.find('[data-testid="queue-nextup"]');
    expect(nextUp.exists()).toBe(true);
    expect(nextUp.text()).toContain('operatorPanel.nextUp');
    expect(nextUp.text()).toContain('OP-100');
    // Remaining quantity is rendered (not just the key): the $t mock
    // interpolates params into the text.
    expect(nextUp.text()).toContain('operatorPanel.remaining');
    expect(nextUp.text()).toContain('78');

    // Second order renders as a plain queue card, not next-up.
    const second = wrapper.find('[data-testid="queue-order-order-2"]');
    expect(second.exists()).toBe(true);
    expect(second.text()).toContain('OP-200');
    expect(second.text()).toContain('40');

    // Andon signal group: one card per Work Center with the category text
    // plus severity text alongside the icon badge (never color alone).
    const group = wrapper.find('[data-testid="signal-machine-1"]');
    expect(group.exists()).toBe(true);
    expect(group.text()).toContain('Downtime');
    expect(group.text()).toContain('operatorPanel.openSignals');
  });

  it('labels the Released row claim and the InProgress row report (claim is the first Confirmation)', async () => {
    const wrapper = mountPanel();
    await flushPromises();
    await loadQueueFor(wrapper);

    // The queue only ever contains Released/InProgress rows, so Release is
    // intentionally not wired: claiming a Released row opens the
    // Confirmation dialog whose first Confirmation moves it to InProgress.
    const claimBtn = findButtonIn(wrapper, 'queue-nextup', 'operatorPanel.claim');
    expect(claimBtn).toBeDefined();
    const reportBtn = findButtonIn(wrapper, 'queue-order-order-2', 'productionConfirmations.report');
    expect(reportBtn).toBeDefined();
  });

  it('claim on a Released order persists a Confirmation for that order and reloads', async () => {
    const wrapper = mountPanel();
    await flushPromises();
    await loadQueueFor(wrapper);
    const callsBefore = getQueueMock.mock.calls.length;

    await findButtonIn(wrapper, 'queue-nextup', 'operatorPanel.claim')?.trigger('click');
    await flushPromises();

    const form = wrapper.find('#panel-confirm-form');
    expect(form.exists()).toBe(true);
    const quantities = form.findAll('input[type="number"]');
    expect(quantities.length).toBeGreaterThan(0);
    await quantities[0]?.setValue('5');

    await form.trigger('submit');
    await flushPromises();
    await flushPromises();

    expect(confirmCreateMock).toHaveBeenCalledTimes(1);
    expect(confirmCreateMock).toHaveBeenCalledWith(
      expect.objectContaining({
        productionOrderId: 'order-1',
        machineId: 'machine-1',
        goodQuantity: 5
      })
    );
    expect(getQueueMock.mock.calls.length).toBeGreaterThan(callsBefore);
    const toast = useToastStore();
    expect(toast.toasts.some((t) => t.variant === 'success')).toBe(true);
  });

  it('never attributes a confirmation to the wrong operator without an exact identifier match', async () => {
    // Review follow-up on PR #342: resolveOperatorId used to fall back to
    // page.items[0], silently attributing writes to whoever the server
    // returned first. It now fails safe to null (unattributed) instead.
    operatorBrowseMock.mockResolvedValue(
      page([
        {
          id: 'operator-other',
          identifier: 'OP-OTHER',
          firstName: 'Mallory',
          lastName: 'Other',
          ratePerHour: 0
        }
      ])
    );
    const wrapper = mountPanel();
    await flushPromises();
    await loadQueueFor(wrapper);

    await findButtonIn(wrapper, 'queue-nextup', 'operatorPanel.claim')?.trigger('click');
    await flushPromises();

    const form = wrapper.find('#panel-confirm-form');
    expect(form.exists()).toBe(true);
    await form.findAll('input[type="number"]')[0]?.setValue('5');
    await form.trigger('submit');
    await flushPromises();
    await flushPromises();

    expect(confirmCreateMock).toHaveBeenCalledTimes(1);
    expect(confirmCreateMock).toHaveBeenCalledWith(
      expect.objectContaining({ productionOrderId: 'order-1', reportedByOperatorId: null })
    );
  });

  it('confirm on an InProgress order persists another Confirmation for that order', async () => {
    const wrapper = mountPanel();
    await flushPromises();
    await loadQueueFor(wrapper);

    await findButtonIn(wrapper, 'queue-order-order-2', 'productionConfirmations.report')?.trigger('click');
    await flushPromises();

    const form = wrapper.find('#panel-confirm-form');
    expect(form.exists()).toBe(true);
    await form.findAll('input[type="number"]')[0]?.setValue('3');
    await form.trigger('submit');
    await flushPromises();
    await flushPromises();

    expect(confirmCreateMock).toHaveBeenCalledTimes(1);
    expect(confirmCreateMock).toHaveBeenCalledWith(
      expect.objectContaining({ productionOrderId: 'order-2', goodQuantity: 3 })
    );
  });

  it('scrap persists a scrap event with a Reason code for the order', async () => {
    const wrapper = mountPanel();
    await flushPromises();
    await loadQueueFor(wrapper);

    await findButtonIn(wrapper, 'queue-nextup', 'operatorPanel.reportScrap')?.trigger('click');
    await flushPromises();

    const form = wrapper.find('#panel-scrap-form');
    expect(form.exists()).toBe(true);
    // First select is the Work Center (pre-filled), second is the Reason code.
    const selects = form.findAll('select');
    expect(selects.length).toBe(2);
    await selects[1]?.setValue('reason-scrap-1');
    const quantity = form.findAll('input[type="number"]')[0];
    expect(quantity?.exists()).toBe(true);
    await quantity?.setValue('2');

    await form.trigger('submit');
    await flushPromises();
    await flushPromises();

    expect(scrapCreateMock).toHaveBeenCalledTimes(1);
    expect(scrapCreateMock).toHaveBeenCalledWith(
      expect.objectContaining({
        reasonCodeId: 'reason-scrap-1',
        quantity: 2,
        productionOrderId: 'order-1'
      })
    );
  });

  it('downtime starts a downtime event with a Reason code for the order', async () => {
    const wrapper = mountPanel();
    await flushPromises();
    await loadQueueFor(wrapper);

    await findButtonIn(wrapper, 'queue-nextup', 'operatorPanel.reportDowntime')?.trigger('click');
    await flushPromises();

    const form = wrapper.find('#panel-downtime-form');
    expect(form.exists()).toBe(true);
    // First select is the Work Center (pre-filled), second is the Reason code.
    const selects = form.findAll('select');
    expect(selects.length).toBe(2);
    await selects[1]?.setValue('reason-down-1');

    await form.trigger('submit');
    await flushPromises();
    await flushPromises();

    expect(downtimeStartMock).toHaveBeenCalledTimes(1);
    expect(downtimeStartMock).toHaveBeenCalledWith(
      expect.objectContaining({
        reasonCodeId: 'reason-down-1',
        productionOrderId: 'order-1'
      })
    );
  });

  it('acknowledge calls the Andon endpoint and reloads the queue', async () => {
    const wrapper = mountPanel();
    await flushPromises();
    await loadQueueFor(wrapper);
    const callsBefore = getQueueMock.mock.calls.length;

    const ack = findButton(wrapper, 'andon.acknowledge');
    expect(ack).toBeDefined();
    await ack?.trigger('click');
    await flushPromises();
    await flushPromises();

    expect(acknowledgeMock).toHaveBeenCalledWith('signal-1');
    expect(getQueueMock.mock.calls.length).toBeGreaterThan(callsBefore);
  });

  it('off-shift renders the empty queue, not an error', async () => {
    getQueueMock.mockResolvedValue({ ...queueFixture(), shift: null, orders: [], activeSignals: [] });
    const wrapper = mountPanel();
    await flushPromises();
    await loadQueueFor(wrapper);
    const toast = useToastStore();

    expect(wrapper.text()).toContain('operatorPanel.offShift');
    expect(wrapper.text()).toContain('operatorPanel.queueEmpty');
    expect(toast.toasts.filter((t) => t.variant === 'error')).toHaveLength(0);
  });

  it('keeps lookup browses within the server pageSize caps', async () => {
    const wrapper = mountPanel();
    await flushPromises();
    expect(machineBrowseMock).toHaveBeenCalled();
    expect(reasonBrowseMock).toHaveBeenCalled();
    for (const call of machineBrowseMock.mock.calls) {
      expect((call[0] as { pageSize?: number }).pageSize).toBeLessThanOrEqual(100);
    }
    for (const call of reasonBrowseMock.mock.calls) {
      expect((call[0] as { pageSize?: number }).pageSize).toBeLessThanOrEqual(100);
    }
    wrapper.unmount();
  });

  it('wires the tablet single-column layout (1024x768: no overlap, no horizontal action scroll)', () => {
    const text = viewSources['./OperatorPanelView.vue'] ?? '';

    // Shared touch scope drives 44px targets through the view class.
    expect(text).toContain('useShopfloorDensity');
    expect(text).toContain(':class="viewClass"');
    // Tablet breakpoint collapses grids to one column and stretches each
    // primary action full-width with wrapping, so actions cannot overlap
    // or force horizontal scrolling at 1024x768.
    expect(text).toMatch(/@media\s*\(\s*max-width\s*:\s*1100px\s*\)/);
    expect(text).toContain('.panel-grid { grid-template-columns: 1fr; }');
    expect(text).toContain('.form-grid { grid-template-columns: 1fr; }');
    expect(text).toContain('.panel-card__actions > * { flex: 1 1 100%; }');
    expect(text).toContain('flex-wrap: wrap');
  });
});
