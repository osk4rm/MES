import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import SpcCharacteristicsView from './SpcCharacteristicsView.vue';
import { spcCharacteristicService, SpcChartType } from '../../services/spcCharacteristicService';
import { spcMeasurementService } from '../../services/spcMeasurementService';
import { productService } from '../../services/productService';
import { machineService } from '../../services/machineService';

// Verifier gaps for #188 / PR #193 (TESTS_INSUFFICIENT): the service spec only
// proved URL/params mapping and the chart spec fed pre-sorted input, leaving
// the view wiring unasserted — 404 -> not-found state, range change -> BOTH
// endpoints with the same range, empty chart+page -> empty state, and the
// close-during-fetch trap. These component tests close those gaps alongside
// the strengthened SpcControlChart order test. JWT attachment lives in the
// shared `http` interceptor (the service delegates to it); here we prove the
// cross-tenant consequence: a 404 surfaces the not-found state instead of a
// broken chart.

vi.mock('../../services/spcCharacteristicService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/spcCharacteristicService')>();
  return {
    ...actual,
    spcCharacteristicService: {
      browse: vi.fn(),
      get: vi.fn(),
      create: vi.fn(),
      update: vi.fn(),
      remove: vi.fn()
    }
  };
});

vi.mock('../../services/spcMeasurementService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/spcMeasurementService')>();
  return {
    ...actual,
    spcMeasurementService: {
      browse: vi.fn(),
      getChart: vi.fn(),
      get: vi.fn()
    }
  };
});

vi.mock('../../services/productService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/productService')>();
  return {
    ...actual,
    productService: {
      browse: vi.fn()
    }
  };
});

vi.mock('../../services/machineService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/machineService')>();
  return {
    ...actual,
    machineService: {
      browse: vi.fn()
    }
  };
});

vi.mock('vue-i18n', () => ({
  useI18n: (): {
    t: (key: string, params?: Record<string, unknown>) => string;
    tm: (key: string) => Record<string, string>;
  } => ({
    t: (key: string): string => key,
    tm: (key: string): Record<string, string> =>
      key === 'spcCharacteristics.chartTypes'
        ? { '1': 'XbarR', '2': 'XbarS', '3': 'XmR', '4': 'P', '5': 'C' }
        : {}
  })
}));

const browseCharacteristicsMock = vi.mocked(spcCharacteristicService.browse);
const browseMeasurementsMock = vi.mocked(spcMeasurementService.browse);
const getChartMock = vi.mocked(spcMeasurementService.getChart);
const browseProductsMock = vi.mocked(productService.browse);
const browseMachinesMock = vi.mocked(machineService.browse);

function characteristic(): Record<string, unknown> {
  return {
    id: 'characteristic-1',
    code: 'SPC-1',
    name: 'Diameter',
    description: null,
    productId: null,
    machineId: null,
    chartType: SpcChartType.XbarR,
    nominalValue: 10,
    lowerSpecLimit: 9,
    upperSpecLimit: 11,
    lowerControlLimit: 9.5,
    upperControlLimit: 10.5,
    sampleSize: 5,
    unit: 'mm',
    isActive: true
  };
}

function measurementRow(overrides: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    id: 'measurement-1',
    characteristicId: 'characteristic-1',
    value: 10.1,
    measuredAt: new Date('2026-09-24T10:00:00Z').toISOString(),
    notes: null,
    createdAt: new Date('2026-09-24T10:00:00Z').toISOString(),
    updatedAt: null,
    ...overrides
  };
}

function chartPayload(overrides: Record<string, unknown> = {}): Record<string, unknown> {
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
        value: 10.1,
        measuredAt: new Date('2026-09-24T10:00:00Z').toISOString(),
        isOutOfControl: false,
        isOutOfSpec: false
      },
      {
        id: 'measurement-2',
        value: 12,
        measuredAt: new Date('2026-09-24T10:05:00Z').toISOString(),
        isOutOfControl: true,
        isOutOfSpec: true
      }
    ],
    totalCount: 2,
    outOfControlCount: 1,
    outOfSpecCount: 1,
    ...overrides
  };
}

function notFoundError(): unknown {
  return { response: { status: 404, data: { title: 'Not found' } }, message: 'Request failed with status code 404' };
}

function mountCharacteristics(): VueWrapper {
  return mount(SpcCharacteristicsView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key },
      stubs: {
        // Render modal content inline so Teleport does not move it to
        // document.body (unreachable via wrapper.find in jsdom).
        AppModal: {
          props: ['open', 'title', 'size'],
          template: '<div v-if="open" class="modal-stub"><slot /><slot name="footer" /></div>'
        },
        AppPageHeader: true,
        AppPagination: true,
        AppConfirmDialog: true
      }
    }
  }) as unknown as VueWrapper;
}

async function openMeasurementsDialog(wrapper: VueWrapper): Promise<void> {
  const rowButton = wrapper.find('.app-row-actions__btn');
  expect(rowButton.exists()).toBe(true);
  await rowButton.trigger('click');
  await flushPromises();
}

describe('SpcCharacteristicsView measurements dialog', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    vi.useRealTimers();
    browseCharacteristicsMock.mockResolvedValue({
      items: [characteristic() as never],
      totalCount: 1,
      totalPages: 1
    });
    browseProductsMock.mockResolvedValue({ items: [], totalCount: 0, totalPages: 0 });
    browseMachinesMock.mockResolvedValue({ items: [], totalCount: 0, totalPages: 0 });
  });

  it('range Apply reloads BOTH the log and the chart with the identical range', async () => {
    browseMeasurementsMock.mockResolvedValue({
      items: [measurementRow(), measurementRow({ id: 'measurement-2', value: 12 })],
      totalCount: 2,
      totalPages: 1
    } as never);
    getChartMock.mockResolvedValue(chartPayload() as never);

    const wrapper = mountCharacteristics();
    await flushPromises();
    await openMeasurementsDialog(wrapper);

    expect(browseMeasurementsMock).toHaveBeenCalledTimes(1);
    expect(getChartMock).toHaveBeenCalledTimes(1);

    const inputs = wrapper.findAll('input[type="datetime-local"]');
    expect(inputs).toHaveLength(2);
    await inputs[0]?.setValue('2026-09-24T06:00');
    await inputs[1]?.setValue('2026-09-24T14:00');

    const apply = wrapper.findAll('button').find((b) => b.text().includes('spcMeasurements.apply'));
    expect(apply?.exists()).toBe(true);
    await apply?.trigger('click');
    await flushPromises();

    expect(browseMeasurementsMock).toHaveBeenCalledTimes(2);
    expect(getChartMock).toHaveBeenCalledTimes(2);

    const lastBrowse = browseMeasurementsMock.mock.calls[1]?.[0] as unknown as Record<string, unknown>;
    const lastChart = getChartMock.mock.calls[1]?.[0] as unknown as Record<string, unknown>;
    expect(lastBrowse.characteristicId).toBe('characteristic-1');
    expect(lastChart.characteristicId).toBe('characteristic-1');
    // Both endpoints share the identical range so log and chart stay in sync.
    expect(typeof lastBrowse.from).toBe('string');
    expect(typeof lastBrowse.to).toBe('string');
    expect(lastChart.from).toBe(lastBrowse.from);
    expect(lastChart.to).toBe(lastBrowse.to);

    // Summary, chart and joined log flags render together.
    expect(wrapper.text()).toContain('spcMeasurements.summary');
    expect(wrapper.find('[data-testid="spc-chart-svg"]').exists()).toBe(true);
    expect(wrapper.text()).toContain('spcMeasurements.outOfControl');
  });

  it('shows the not-found state instead of a broken chart on 404 (unknown/cross-tenant id)', async () => {
    browseMeasurementsMock.mockRejectedValue(notFoundError());
    getChartMock.mockResolvedValue(chartPayload() as never);

    const wrapper = mountCharacteristics();
    await flushPromises();
    await openMeasurementsDialog(wrapper);

    expect(wrapper.text()).toContain('spcMeasurements.notFound');
    expect(wrapper.text()).toContain('spcMeasurements.notFoundHint');
    expect(wrapper.find('[data-testid="spc-chart-svg"]').exists()).toBe(false);
    expect(wrapper.text()).not.toContain('spcMeasurements.summary');
  });

  it('shows the empty state when the chart and the log page are both empty', async () => {
    browseMeasurementsMock.mockResolvedValue({ items: [], totalCount: 0, totalPages: 0 } as never);
    getChartMock.mockResolvedValue(chartPayload({ points: [], totalCount: 0, outOfControlCount: 0, outOfSpecCount: 0 }) as never);

    const wrapper = mountCharacteristics();
    await flushPromises();
    await openMeasurementsDialog(wrapper);

    expect(wrapper.text()).toContain('spcMeasurements.noMeasurements');
    expect(wrapper.find('[data-testid="spc-chart-svg"]').exists()).toBe(false);
  });

  it('closing during a slow fetch does not trap the dialog and ignores the late response', async () => {
    let resolveBrowse!: (value: unknown) => void;
    browseMeasurementsMock.mockReturnValue(
      new Promise((resolve) => {
        resolveBrowse = resolve;
      }) as never
    );
    getChartMock.mockResolvedValue(chartPayload() as never);

    const wrapper = mountCharacteristics();
    await flushPromises();

    const rowButton = wrapper.find('.app-row-actions__btn');
    await rowButton.trigger('click');
    await flushPromises();
    // Fetch is in flight: the dialog is open.
    expect(wrapper.findAll('.modal-stub')).toHaveLength(1);

    const close = wrapper.findAll('button').find((b) => b.text().includes('common.close'));
    expect(close?.exists()).toBe(true);
    await close?.trigger('click');
    await flushPromises();

    // Dialog closes even while loading — never trapped open.
    expect(wrapper.findAll('.modal-stub')).toHaveLength(0);

    resolveBrowse({ items: [measurementRow()], totalCount: 1, totalPages: 1 });
    await flushPromises();

    // Late response is ignored: dialog stays closed, no chart leaks in.
    expect(wrapper.findAll('.modal-stub')).toHaveLength(0);
    expect(wrapper.find('[data-testid="spc-chart-svg"]').exists()).toBe(false);
  });

  it('renders the log table in ascending time order even when the browse payload is unsorted', async () => {
    const early = measurementRow({
      id: 'measurement-early',
      value: 10.1,
      measuredAt: new Date('2026-09-24T10:00:00Z').toISOString()
    });
    const late = measurementRow({
      id: 'measurement-late',
      value: 12,
      measuredAt: new Date('2026-09-24T10:05:00Z').toISOString()
    });
    // Payload arrives late-first; the view must still render early-first.
    browseMeasurementsMock.mockResolvedValue({ items: [late, early], totalCount: 2, totalPages: 1 } as never);
    getChartMock.mockResolvedValue(chartPayload() as never);

    const wrapper = mountCharacteristics();
    await flushPromises();
    await openMeasurementsDialog(wrapper);

    const logRows = wrapper.findAll('.app-table__row');
    // First row is the characteristic row; the rest belong to the log table.
    expect(logRows.length).toBeGreaterThanOrEqual(3);
    const logTexts = logRows.slice(1).map((r) => r.text());
    expect(logTexts).toHaveLength(2);
    expect(logTexts[0]).toContain('10.1');
    expect(logTexts[1]).toContain('12');
  });
});
